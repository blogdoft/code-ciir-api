using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Projects;
using CodeCiir.Embeddings.Abstraction;
using CodeCiir.Reranking.Abstraction;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.CodeQueries;

public sealed class CodeQueryServiceTests
{
    private static readonly Project TestProject = new(
        1,
        "proj",
        "bge-m3",
        1024,
        new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
        new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"),
        DateTime.UtcNow,
        DateTime.UtcNow);

    private readonly IProjectsRepository _projectsRepository = Substitute.For<IProjectsRepository>();
    private readonly ICodeDocumentsRepository _codeDocumentsRepository = Substitute.For<ICodeDocumentsRepository>();
    private readonly IRelationshipGraphRepository _relationshipGraphRepository = Substitute.For<IRelationshipGraphRepository>();
    private readonly IReranker _reranker = Substitute.For<IReranker>();
    private readonly CodeQueryService _sut;

    public CodeQueryServiceTests()
    {
        var providerFactory = Substitute.For<IEmbeddingProviderFactory>();
        var generator = Substitute.For<IEmbeddingGenerator>();
        providerFactory.ProviderName.Returns("Ollama");
        providerFactory.Create(Arg.Any<EmbeddingOptions>(), Arg.Any<string>(), Arg.Any<int>()).Returns(generator);
        generator.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new EmbeddingVector([0.1f]));

        var embeddingOptions = Options.Create(new EmbeddingOptions
        {
            Provider = "Ollama",
            BaseUrl = "http://localhost:11434",
            Model = "bge-m3",
            Dimensions = 1024,
        });
        var resolver = new EmbeddingGeneratorResolver([providerFactory], embeddingOptions);

        // Passthrough by default, mirroring a disabled (NoOp) reranker - individual tests
        // override this when they need to assert on reranking-specific behavior.
        _reranker.Provider.Returns("None");
        _reranker.CandidatePoolSize.Returns(0);
        _reranker.RerankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<RerankCandidate>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<IReadOnlyList<RerankCandidate>>().Select(c => new RerankedCandidate(c.Id, null)).ToList());

        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _relationshipGraphRepository
            .GetGraphAsync(Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(CodeGraph.Empty);

        _sut = new CodeQueryService(_projectsRepository, _codeDocumentsRepository, _relationshipGraphRepository, resolver, embeddingOptions, _reranker);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task QueryAsync_MissingOrBlankQuestion_ReturnsQuestionRequired(string? question)
    {
        var result = await _sut.QueryAsync(question, projectId: 1);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-question-required");
        await _projectsRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_QuestionTooLong_ReturnsQuestionTooLong()
    {
        var tooLong = new string('a', CodeQueryService.MaxQuestionLength + 1);

        var result = await _sut.QueryAsync(tooLong, projectId: 1);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-question-too-long");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task QueryAsync_ProjectIdNotPositive_ReturnsProjectIdInvalid(long invalidProjectId)
    {
        var result = await _sut.QueryAsync("question", invalidProjectId);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-project-id-invalid");
        await _projectsRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task QueryAsync_MinSimilarityOutOfRange_ReturnsMinSimilarityOutOfRange(double invalidMinSimilarity)
    {
        var result = await _sut.QueryAsync("question", minSimilarity: invalidMinSimilarity);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-min-similarity-out-of-range");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task QueryAsync_BlankKind_ReturnsKindFilterValueRequired(string blankKind)
    {
        var result = await _sut.QueryAsync("question", kind: blankKind);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-kind-filter-value-required");
    }

    [Fact]
    public async Task QueryAsync_KindTooLong_ReturnsKindFilterValueTooLong()
    {
        var tooLong = new string('a', CodeQueryService.MaxFilterValueLength + 1);

        var result = await _sut.QueryAsync("question", kind: tooLong);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-kind-filter-value-too-long");
    }

    [Fact]
    public async Task QueryAsync_QualifiedNameOperatorWithoutValue_ReturnsQualifiedNameFilterValueRequired()
    {
        var result = await _sut.QueryAsync("question", qualifiedNameOperator: QualifiedNameFilterOperator.Equals, qualifiedNameValue: null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-qualified-name-filter-value-required");
    }

    [Fact]
    public async Task QueryAsync_QualifiedNameValueTooLong_ReturnsQualifiedNameFilterValueTooLong()
    {
        var tooLong = new string('a', CodeQueryService.MaxFilterValueLength + 1);

        var result = await _sut.QueryAsync("question", qualifiedNameOperator: QualifiedNameFilterOperator.Contains, qualifiedNameValue: tooLong);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-qualified-name-filter-value-too-long");
    }

    [Fact]
    public async Task QueryAsync_ProjectIdGiven_MissingProject_ReturnsProjectNotFound()
    {
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.QueryAsync("where is the retry logic?", projectId: 1);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("404-project-not-found");
    }

    [Fact]
    public async Task QueryAsync_ProjectIdOmitted_SkipsProjectLookup()
    {
        var result = await _sut.QueryAsync("question");

        result.IsSuccess.ShouldBeTrue();
        await _projectsRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ValidQuestion_EmbedsWithConfiguredModelAndSearchesWithDefaultLimit()
    {
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestProject);
        var searched = new[] { new CodeQueryResult(1, "method", null, "Foo", null, null, null, null, 0.9) };
        var expected = new[] { searched[0] with { Relations = [] } };
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), null, 1, null, null, null, CodeQueryService.DefaultLimit, Arg.Any<CancellationToken>())
            .Returns(searched);

        var result = await _sut.QueryAsync("where is the retry logic?", projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1000, CodeQueryService.MaxResultLimit)]
    public async Task QueryAsync_LimitOutOfRange_IsClamped(int requestedLimit, int expectedEffectiveLimit)
    {
        await _sut.QueryAsync("question", limit: requestedLimit);

        await _codeDocumentsRepository.Received(1).SearchAsync(
            Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), expectedEffectiveLimit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_AllFiltersProvided_PassesThemToRepository()
    {
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestProject);

        var result = await _sut.QueryAsync(
            "question",
            projectId: 1,
            minSimilarity: 0.5,
            kind: "method",
            qualifiedNameOperator: QualifiedNameFilterOperator.Contains,
            qualifiedNameValue: "*Foo*",
            limit: 20);

        result.IsSuccess.ShouldBeTrue();
        await _codeDocumentsRepository.Received(1).SearchAsync(
            Arg.Any<IReadOnlyList<float>>(), 0.5, 1, "method", QualifiedNameFilterOperator.Contains, "*Foo*", 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_RerankerRequestsLargerPool_WidensSearchLimit()
    {
        _reranker.CandidatePoolSize.Returns(25);

        await _sut.QueryAsync("question", limit: 10);

        await _codeDocumentsRepository.Received(1).SearchAsync(
            Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_RerankerRequestsExcessivePoolSize_CapsAtMaxCandidatePoolSize()
    {
        _reranker.CandidatePoolSize.Returns(10_000);

        await _sut.QueryAsync("question", limit: 10);

        await _codeDocumentsRepository.Received(1).SearchAsync(
            Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), CodeQueryService.MaxCandidatePoolSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_RerankerReordersCandidates_ResultsReflectRerankScoreDescending()
    {
        var low = new CodeQueryResult(1, "method", null, "Low", null, null, null, "low", 0.95);
        var high = new CodeQueryResult(2, "method", null, "High", null, null, null, "high", 0.10);
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([low, high]);
        _reranker.RerankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<RerankCandidate>>(), Arg.Any<CancellationToken>())
            .Returns([new RerankedCandidate(1, 0.1), new RerankedCandidate(2, 0.9)]);

        var result = await _sut.QueryAsync("question");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Select(m => m.Id).ShouldBe([2, 1]);
        result.Value.Matches.Single(m => m.Id == 2).RerankScore.ShouldBe(0.9);
    }

    [Fact]
    public async Task QueryAsync_RerankerDisabled_RerankScoreIsNullAndSimilarityOrderIsKept()
    {
        var first = new CodeQueryResult(1, "method", null, "First", null, null, null, "first", 0.95);
        var second = new CodeQueryResult(2, "method", null, "Second", null, null, null, "second", 0.80);
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);

        var result = await _sut.QueryAsync("question");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Select(m => m.Id).ShouldBe([1, 2]);
        result.Value.Matches.ShouldAllBe(m => m.RerankScore == null);
    }

    [Fact]
    public async Task QueryAsync_MoreCandidatesThanLimit_TruncatesAfterReranking()
    {
        var candidates = Enumerable.Range(1, 5)
            .Select(id => new CodeQueryResult(id, "method", null, $"M{id}", null, null, null, $"text{id}", 0.5))
            .ToList();
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(candidates);

        var result = await _sut.QueryAsync("question", limit: 2);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Count.ShouldBe(2);
    }

    [Fact]
    public async Task QueryAsync_MatchesFound_ProjectIdGiven_ExpandsGraphFromMatchIds()
    {
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestProject);
        var match = new CodeQueryResult(42, "method", null, "Foo", null, null, null, null, 0.9);
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), 1, Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([match]);
        var expectedGraph = new CodeGraph([new GraphNode(7, "method", null, "Bar", null, null, null, 1)], [], false);
        _relationshipGraphRepository
            .GetGraphAsync(1, Arg.Is<IReadOnlyList<long>>(ids => ids.SequenceEqual(new long[] { 42 })), CodeQueryService.MaxGraphDepth, CodeQueryService.MaxGraphNodes, Arg.Any<CancellationToken>())
            .Returns(expectedGraph);

        var result = await _sut.QueryAsync("question", projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Graph.ShouldBe(expectedGraph);
    }

    [Fact]
    public async Task QueryAsync_MatchesFound_ProjectIdOmitted_SkipsGraphExpansion()
    {
        var match = new CodeQueryResult(42, "method", null, "Foo", null, null, null, null, 0.9);
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), null, Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([match]);

        var result = await _sut.QueryAsync("question");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Graph.ShouldBe(CodeGraph.Empty);
        await _relationshipGraphRepository.DidNotReceive().GetGraphAsync(
            Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_NoMatches_SkipsGraphExpansion()
    {
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestProject);

        var result = await _sut.QueryAsync("question", projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Graph.ShouldBe(CodeGraph.Empty);
        await _relationshipGraphRepository.DidNotReceive().GetGraphAsync(
            Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_MatchesFound_ProjectIdGiven_PopulatesRelationsFromRepository()
    {
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestProject);
        var match = new CodeQueryResult(42, "method", null, "Foo", null, null, null, null, 0.9);
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), 1, Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([match]);
        var relation = new MatchRelation(42, 7, "calls", "Bar", "project");
        _relationshipGraphRepository
            .GetDirectRelationsAsync(1, Arg.Is<IReadOnlyList<long>>(ids => ids.SequenceEqual(new long[] { 42 })), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, IReadOnlyList<MatchRelation>> { [42] = [relation] });

        var result = await _sut.QueryAsync("question", projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Single().Relations.ShouldBe([relation]);
    }

    [Fact]
    public async Task QueryAsync_MatchWithNoDirectRelations_RelationsIsEmptyArray()
    {
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestProject);
        var match = new CodeQueryResult(42, "method", null, "Foo", null, null, null, null, 0.9);
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), 1, Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([match]);
        _relationshipGraphRepository
            .GetDirectRelationsAsync(1, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, IReadOnlyList<MatchRelation>>());

        var result = await _sut.QueryAsync("question", projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Single().Relations.ShouldBeEmpty();
    }

    [Fact]
    public async Task QueryAsync_MatchesFound_ProjectIdOmitted_RelationsAreEmptyAndRepositoryNotCalled()
    {
        var match = new CodeQueryResult(42, "method", null, "Foo", null, null, null, null, 0.9);
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), null, Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([match]);

        var result = await _sut.QueryAsync("question");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Single().Relations.ShouldBeEmpty();
        await _relationshipGraphRepository.DidNotReceive().GetDirectRelationsAsync(
            Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_NoMatches_SkipsDirectRelationsLookup()
    {
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestProject);

        var result = await _sut.QueryAsync("question", projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        await _relationshipGraphRepository.DidNotReceive().GetDirectRelationsAsync(
            Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>());
    }
}
