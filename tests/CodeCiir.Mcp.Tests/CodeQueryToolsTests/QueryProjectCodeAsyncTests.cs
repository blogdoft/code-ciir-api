using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.CodeQueries;
using CodeCiir.Mcp.Tests.Support;
using ModelContextProtocol;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Mcp.Tests.CodeQueryToolsTests;

public sealed class QueryProjectCodeAsyncTests : BaseCodeQueryToolsTests
{
    [Fact]
    public async Task Should_PassEveryParameterThroughAndMapMatchesAndGraph_When_ServiceSucceeds()
    {
        var projectId = Guid.NewGuid();
        var relation = new MatchRelation(1, 2, "calls", "C.N", "project");
        var match = CodeQueryResultFaker.Create() with { Id = 1, Relations = [relation] };
        var node = new GraphNode(2, "method", "C", "N", "C.N", "C.N()", "f2.cs", 1);
        var edge = new GraphEdge(1, 2, "calls", "C.N", "project", 0);
        CodeQueryService.QueryAsync(
            "question", projectId, 0.5, "method", QualifiedNameFilterOperator.Contains, "*Foo*", 5, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromSuccess(new CodeQueryResponse([match], new CodeGraph([node], [edge], false))));

        var result = await Sut.QueryProjectCodeAsync(
            "question", projectId, 0.5, "method", QualifiedNameFilterOperator.Contains, "*Foo*", 5);

        result.Matches.Select(m => m.Id).ShouldBe([1L]);
        result.Matches[0].Relations.Select(r => r.RelationType).ShouldBe(["calls"]);
        result.Graph.Nodes.Select(n => n.Id).ShouldBe([2L]);
        result.Graph.Edges.Select(e => e.RelationType).ShouldBe(["calls"]);
        result.Graph.Truncated.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_PassNullProjectIdThrough_When_ProjectIdIsOmitted()
    {
        CodeQueryService.QueryAsync(
            "question", null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromSuccess(new CodeQueryResponse([], CodeGraph.Empty)));

        var result = await Sut.QueryProjectCodeAsync("question");

        result.Matches.ShouldBeEmpty();
        await CodeQueryService.Received(1).QueryAsync(
            "question", null, null, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowMcpExceptionWithTheFailureMessage_When_ServiceFails()
    {
        var failure = CodeQueryFailures.QuestionRequired();
        CodeQueryService.QueryAsync(
            Arg.Any<string>(),
            Arg.Any<Guid?>(),
            Arg.Any<double?>(),
            Arg.Any<string?>(),
            Arg.Any<QualifiedNameFilterOperator?>(),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromFailure(failure));

        var exception = await Should.ThrowAsync<McpException>(() => Sut.QueryProjectCodeAsync("  "));

        exception.Message.ShouldContain(failure.Message);
    }
}
