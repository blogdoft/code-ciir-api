using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Feedback;
using CodeCiir.Mcp.Tools;
using ModelContextProtocol;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Mcp.Tests;

public sealed class CodeQueryToolsTests
{
    private readonly ICodeQueryService _codeQueryService = Substitute.For<ICodeQueryService>();
    private readonly IFeedbackService _feedbackService = Substitute.For<IFeedbackService>();
    private readonly CodeQueryTools _sut;

    public CodeQueryToolsTests()
    {
        _sut = new CodeQueryTools(_codeQueryService, _feedbackService);
    }

    [Fact]
    public async Task QueryProjectCodeAsync_PassesParametersThroughAndMapsMatchesAndGraph()
    {
        var match = new CodeQueryResult(1, "method", "C", "M", "C.M", "C.M()", "f.cs", "text", 0.9)
            with
        { Relations = [new MatchRelation(1, 2, "calls", "C.N", "project")] };
        var node = new GraphNode(2, "method", "C", "N", "C.N", "C.N()", "f2.cs", 1);
        var edge = new GraphEdge(1, 2, "calls", "C.N", "project", 0);
        _codeQueryService.QueryAsync(
            "question", 7, 0.5, "method", QualifiedNameFilterOperator.Contains, "*Foo*", 5, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromSuccess(new CodeQueryResponse([match], new CodeGraph([node], [edge], false))));

        var result = await _sut.QueryProjectCodeAsync(
            "question", 7, 0.5, "method", QualifiedNameFilterOperator.Contains, "*Foo*", 5);

        result.Matches.ShouldHaveSingleItem();
        result.Matches[0].Id.ShouldBe(1);
        result.Matches[0].Relations.ShouldHaveSingleItem();
        result.Matches[0].Relations[0].RelationType.ShouldBe("calls");
        result.Graph.Nodes.ShouldHaveSingleItem();
        result.Graph.Edges.ShouldHaveSingleItem();
        result.Graph.Edges[0].RelationType.ShouldBe("calls");
        result.Graph.Truncated.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryProjectCodeAsync_ProjectIdOmitted_PassesNullThrough()
    {
        _codeQueryService.QueryAsync(
            "question", null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromSuccess(new CodeQueryResponse([], CodeGraph.Empty)));

        var result = await _sut.QueryProjectCodeAsync("question");

        result.Matches.ShouldBeEmpty();
        await _codeQueryService.Received(1).QueryAsync(
            "question", null, null, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryProjectCodeAsync_FailureResult_ThrowsMcpException()
    {
        _codeQueryService.QueryAsync(
            Arg.Any<string>(),
            Arg.Any<long?>(),
            Arg.Any<double?>(),
            Arg.Any<string?>(),
            Arg.Any<QualifiedNameFilterOperator?>(),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromFailure(CodeQueryFailures.QuestionRequired()));

        await Should.ThrowAsync<McpException>(() => _sut.QueryProjectCodeAsync("  "));
    }

    [Fact]
    public async Task SubmitCodeQueryFeedbackAsync_PassesParametersThrough()
    {
        var feedback = new FeedbackResult(1, 7, "question", true, [0.9], null, "claude code", DateTime.UtcNow);
        _feedbackService.SubmitAsync(7, "question", true, Arg.Any<IReadOnlyList<double>>(), null, "claude code", Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromSuccess(feedback));

        var result = await _sut.SubmitCodeQueryFeedbackAsync(7, "question", true, [0.9], "claude code");

        result.Id.ShouldBe(1);
        result.User.ShouldBe("claude code");
    }

    [Fact]
    public async Task SubmitCodeQueryFeedbackAsync_FailureResult_ThrowsMcpException()
    {
        _feedbackService.SubmitAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<double>>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromFailure(FeedbackFailures.UserRequired()));

        await Should.ThrowAsync<McpException>(() => _sut.SubmitCodeQueryFeedbackAsync(1, "question", true, [], string.Empty));
    }
}
