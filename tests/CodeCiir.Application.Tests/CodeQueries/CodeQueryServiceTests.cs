using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Projects;
using CodeCiir.Application.Tests.Support;
using CodeCiir.Embeddings.Abstraction;
using CodeCiir.Reranking.Abstraction;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.CodeQueries;

public sealed class CodeQueryServiceTests
{
    private const string Question = "where is the retry logic?";

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

        GivenSearchReturns();

        _relationshipGraphRepository
            .GetGraphAsync(Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(CodeGraph.Empty);

        _sut = new CodeQueryService(_projectsRepository, _codeDocumentsRepository, _relationshipGraphRepository, resolver, embeddingOptions, _reranker);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnQuestionRequiredFailure_When_QuestionIsMissingOrBlank(string? question)
    {
        var result = await _sut.QueryAsync(question, projectId: 1);

        result.ShouldBeFailure(CodeQueryFailures.QuestionRequired());
        await _projectsRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnQuestionTooLongFailure_When_QuestionExceedsMaximumLength()
    {
        var tooLong = new string('a', CodeQueryService.MaxQuestionLength + 1);

        var result = await _sut.QueryAsync(tooLong, projectId: 1);

        result.ShouldBeFailure(CodeQueryFailures.QuestionTooLong(CodeQueryService.MaxQuestionLength));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Should_ReturnProjectIdInvalidFailure_When_ProjectIdIsNotPositive(long invalidProjectId)
    {
        var result = await _sut.QueryAsync(Question, invalidProjectId);

        result.ShouldBeFailure(CodeQueryFailures.ProjectIdInvalid());
        await _projectsRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task Should_ReturnMinSimilarityOutOfRangeFailure_When_MinSimilarityIsOutsideZeroToOne(double invalidMinSimilarity)
    {
        var result = await _sut.QueryAsync(Question, minSimilarity: invalidMinSimilarity);

        result.ShouldBeFailure(CodeQueryFailures.MinSimilarityOutOfRange());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnKindFilterValueRequiredFailure_When_KindIsBlank(string blankKind)
    {
        var result = await _sut.QueryAsync(Question, kind: blankKind);

        result.ShouldBeFailure(CodeQueryFailures.KindFilterValueRequired());
    }

    [Fact]
    public async Task Should_ReturnKindFilterValueTooLongFailure_When_KindExceedsMaximumLength()
    {
        var tooLong = new string('a', CodeQueryService.MaxFilterValueLength + 1);

        var result = await _sut.QueryAsync(Question, kind: tooLong);

        result.ShouldBeFailure(CodeQueryFailures.KindFilterValueTooLong(CodeQueryService.MaxFilterValueLength));
    }

    [Fact]
    public async Task Should_ReturnQualifiedNameFilterValueRequiredFailure_When_OperatorIsGivenWithoutValue()
    {
        var result = await _sut.QueryAsync(Question, qualifiedNameOperator: QualifiedNameFilterOperator.Equals, qualifiedNameValue: null);

        result.ShouldBeFailure(CodeQueryFailures.QualifiedNameFilterValueRequired());
    }

    [Fact]
    public async Task Should_ReturnQualifiedNameFilterValueTooLongFailure_When_ValueExceedsMaximumLength()
    {
        var tooLong = new string('a', CodeQueryService.MaxFilterValueLength + 1);

        var result = await _sut.QueryAsync(Question, qualifiedNameOperator: QualifiedNameFilterOperator.Contains, qualifiedNameValue: tooLong);

        result.ShouldBeFailure(CodeQueryFailures.QualifiedNameFilterValueTooLong(CodeQueryService.MaxFilterValueLength));
    }

    [Fact]
    public async Task Should_ReturnProjectNotFoundFailure_When_GivenProjectDoesNotExist()
    {
        const long missingProjectId = 1;
        _projectsRepository.GetByIdAsync(missingProjectId, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.QueryAsync(Question, projectId: missingProjectId);

        result.ShouldBeFailure(ProjectFailures.ProjectNotFound(missingProjectId));
    }

    [Fact]
    public async Task Should_SkipProjectLookup_When_ProjectIdIsOmitted()
    {
        var result = await _sut.QueryAsync(Question);

        result.IsSuccess.ShouldBeTrue();
        await _projectsRepository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SearchWithDefaultLimitAndReturnMatches_When_QuestionIsValid()
    {
        GivenProjectExists(1);
        var searched = CodeQueryResultFaker.Create();
        _codeDocumentsRepository
            .SearchAsync(Arg.Any<IReadOnlyList<float>>(), null, 1, null, null, null, CodeQueryService.DefaultLimit, Arg.Any<CancellationToken>())
            .Returns([searched]);

        var result = await _sut.QueryAsync(Question, projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.ShouldBe([searched with { Relations = [] }]);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1000, CodeQueryService.MaxResultLimit)]
    public async Task Should_ClampSearchLimit_When_RequestedLimitIsOutOfRange(int requestedLimit, int expectedEffectiveLimit)
    {
        await _sut.QueryAsync(Question, limit: requestedLimit);

        await _codeDocumentsRepository.Received(1).SearchAsync(
            Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), expectedEffectiveLimit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_PassEveryFilterToRepository_When_AllFiltersAreProvided()
    {
        GivenProjectExists(1);

        var result = await _sut.QueryAsync(
            Question,
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
    public async Task Should_WidenSearchLimit_When_RerankerRequestsLargerCandidatePool()
    {
        _reranker.CandidatePoolSize.Returns(25);

        await _sut.QueryAsync(Question, limit: 10);

        await _codeDocumentsRepository.Received(1).SearchAsync(
            Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_CapSearchLimitAtMaxCandidatePoolSize_When_RerankerRequestsExcessivePoolSize()
    {
        _reranker.CandidatePoolSize.Returns(10_000);

        await _sut.QueryAsync(Question, limit: 10);

        await _codeDocumentsRepository.Received(1).SearchAsync(
            Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), CodeQueryService.MaxCandidatePoolSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_OrderMatchesByRerankScoreDescending_When_RerankerReordersCandidates()
    {
        var lowRerank = CodeQueryResultFaker.Create() with { Id = 1, Similarity = 0.95 };
        var highRerank = CodeQueryResultFaker.Create() with { Id = 2, Similarity = 0.10 };
        GivenSearchReturns(lowRerank, highRerank);
        _reranker.RerankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<RerankCandidate>>(), Arg.Any<CancellationToken>())
            .Returns([new RerankedCandidate(1, 0.1), new RerankedCandidate(2, 0.9)]);

        var result = await _sut.QueryAsync(Question);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Select(m => (m.Id, m.RerankScore)).ShouldBe([(2L, (double?)0.9), (1L, (double?)0.1)]);
    }

    [Fact]
    public async Task Should_KeepSimilarityOrderWithNullRerankScore_When_RerankerIsDisabled()
    {
        var first = CodeQueryResultFaker.Create() with { Id = 1, Similarity = 0.95 };
        var second = CodeQueryResultFaker.Create() with { Id = 2, Similarity = 0.80 };
        GivenSearchReturns(first, second);

        var result = await _sut.QueryAsync(Question);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Select(m => (m.Id, m.RerankScore)).ShouldBe([(1L, (double?)null), (2L, (double?)null)]);
    }

    [Fact]
    public async Task Should_TruncateToLimitAfterReranking_When_ThereAreMoreCandidatesThanLimit()
    {
        GivenSearchReturns([.. CodeQueryResultFaker.Create(5)]);

        var result = await _sut.QueryAsync(Question, limit: 2);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_ExpandGraphFromMatchIds_When_MatchesAreFoundAndProjectIdIsGiven()
    {
        GivenProjectExists(1);
        var match = CodeQueryResultFaker.Create() with { Id = 42 };
        GivenSearchReturns(match);
        var expectedGraph = new CodeGraph([new GraphNode(7, "method", null, "Bar", null, null, null, 1)], [], false);
        _relationshipGraphRepository
            .GetGraphAsync(1, Arg.Is<IReadOnlyList<long>>(ids => ids.SequenceEqual(new long[] { 42 })), CodeQueryService.MaxGraphDepth, CodeQueryService.MaxGraphNodes, Arg.Any<CancellationToken>())
            .Returns(expectedGraph);

        var result = await _sut.QueryAsync(Question, projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Graph.ShouldBe(expectedGraph);
    }

    [Fact]
    public async Task Should_ReturnEmptyGraphWithoutExpanding_When_ProjectIdIsOmitted()
    {
        GivenSearchReturns(CodeQueryResultFaker.Create());

        var result = await _sut.QueryAsync(Question);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Graph.ShouldBe(CodeGraph.Empty);
        await _relationshipGraphRepository.DidNotReceive().GetGraphAsync(
            Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnEmptyGraphWithoutExpanding_When_NoMatchesAreFound()
    {
        GivenProjectExists(1);

        var result = await _sut.QueryAsync(Question, projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Graph.ShouldBe(CodeGraph.Empty);
        await _relationshipGraphRepository.DidNotReceive().GetGraphAsync(
            Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_PopulateMatchRelationsFromRepository_When_MatchesAreFoundAndProjectIdIsGiven()
    {
        GivenProjectExists(1);
        var match = CodeQueryResultFaker.Create() with { Id = 42 };
        GivenSearchReturns(match);
        var relation = new MatchRelation(42, 7, "calls", "Bar", "project");
        _relationshipGraphRepository
            .GetDirectRelationsAsync(1, Arg.Is<IReadOnlyList<long>>(ids => ids.SequenceEqual(new long[] { 42 })), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, IReadOnlyList<MatchRelation>> { [42] = [relation] });

        var result = await _sut.QueryAsync(Question, projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Single().Relations.ShouldBe([relation]);
    }

    [Fact]
    public async Task Should_ReturnEmptyRelations_When_MatchHasNoDirectRelations()
    {
        GivenProjectExists(1);
        GivenSearchReturns(CodeQueryResultFaker.Create());
        _relationshipGraphRepository
            .GetDirectRelationsAsync(1, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, IReadOnlyList<MatchRelation>>());

        var result = await _sut.QueryAsync(Question, projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Single().Relations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnEmptyRelationsWithoutLookup_When_ProjectIdIsOmitted()
    {
        GivenSearchReturns(CodeQueryResultFaker.Create());

        var result = await _sut.QueryAsync(Question);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Matches.Single().Relations.ShouldBeEmpty();
        await _relationshipGraphRepository.DidNotReceive().GetDirectRelationsAsync(
            Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SkipDirectRelationsLookup_When_NoMatchesAreFound()
    {
        GivenProjectExists(1);

        var result = await _sut.QueryAsync(Question, projectId: 1);

        result.IsSuccess.ShouldBeTrue();
        await _relationshipGraphRepository.DidNotReceive().GetDirectRelationsAsync(
            Arg.Any<long>(), Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>());
    }

    private void GivenProjectExists(long projectId) => _projectsRepository
        .GetByIdAsync(projectId, Arg.Any<CancellationToken>())
        .Returns(ProjectFaker.Create() with { Id = projectId });

    private void GivenSearchReturns(params CodeQueryResult[] results) => _codeDocumentsRepository
        .SearchAsync(Arg.Any<IReadOnlyList<float>>(), Arg.Any<double?>(), Arg.Any<long?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns(results);
}
