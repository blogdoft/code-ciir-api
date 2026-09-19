using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.CodeQueries;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeCiir.Api.Tests;

public sealed class CodeQueriesEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task QueryAsync_ValidQuestion_ReturnsOkWithMatchesAndGraph()
    {
        var matches = new[]
        {
            new CodeQueryResult(
                1,
                "method",
                "Foo.Bar",
                "Baz",
                "Foo.Bar.Baz",
                "Foo.Bar.Baz()",
                "src/Baz.cs",
                "text",
                0.87,
                GitUrl: new Uri("https://github.com/acme/widgets"),
                GitRawUrl: new Uri("https://raw.githubusercontent.com/acme/widgets/main/src/Baz.cs"))
                with { Relations = [new MatchRelation(1, 2, "calls", "Foo.Bar.Qux", "project")] },
        };
        var graph = new CodeGraph(
            [new GraphNode(2, "method", "Foo.Bar", "Qux", "Foo.Bar.Qux", null, "src/Qux.cs", 1)],
            [new GraphEdge(1, 2, "calls", "Foo.Bar.Qux", "project", 1)],
            false);
        var codeQueryService = Substitute.For<ICodeQueryService>();
        codeQueryService.QueryAsync(
            "where is Baz?", 1, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromSuccess(new CodeQueryResponse(matches, graph)));

        using var client = CreateClient(codeQueryService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries", UriKind.Relative),
            new { question = "where is Baz?", project_id = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var matchesJson = body.GetProperty("matches");
        matchesJson.GetArrayLength().ShouldBe(1);
        matchesJson[0].GetProperty("symbol_qualified_name").GetString().ShouldBe("Foo.Bar.Baz");
        matchesJson[0].GetProperty("git_url").GetString().ShouldBe("https://github.com/acme/widgets");
        matchesJson[0].GetProperty("git_raw_url").GetString().ShouldBe("https://raw.githubusercontent.com/acme/widgets/main/src/Baz.cs");
        var relationsJson = matchesJson[0].GetProperty("relations");
        relationsJson.GetArrayLength().ShouldBe(1);
        relationsJson[0].GetProperty("relation_type").GetString().ShouldBe("calls");
        relationsJson[0].GetProperty("target_symbol").GetString().ShouldBe("Foo.Bar.Qux");
        var graphJson = body.GetProperty("graph");
        graphJson.GetProperty("nodes").GetArrayLength().ShouldBe(1);
        graphJson.GetProperty("edges")[0].GetProperty("relation_type").GetString().ShouldBe("calls");
        graphJson.GetProperty("truncated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task QueryAsync_ProjectIdOmitted_SearchesAcrossProjects()
    {
        var codeQueryService = Substitute.For<ICodeQueryService>();
        codeQueryService.QueryAsync(
            "where is Baz?", null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromSuccess(new CodeQueryResponse([], CodeGraph.Empty)));

        using var client = CreateClient(codeQueryService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries", UriKind.Relative),
            new { question = "where is Baz?" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await codeQueryService.Received(1).QueryAsync(
            "where is Baz?", null, null, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_BlankQuestion_ReturnsBadRequest()
    {
        var codeQueryService = Substitute.For<ICodeQueryService>();
        codeQueryService.QueryAsync(
            "  ", null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromFailure(CodeQueryFailures.QuestionRequired()));

        using var client = CreateClient(codeQueryService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries", UriKind.Relative),
            new { question = "  " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task QueryAsync_InvalidProjectIdInBody_ReturnsBadRequest()
    {
        var codeQueryService = Substitute.For<ICodeQueryService>();
        codeQueryService.QueryAsync(
            "question", 0, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromFailure(CodeQueryFailures.ProjectIdInvalid()));

        using var client = CreateClient(codeQueryService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries", UriKind.Relative),
            new { question = "question", project_id = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task QueryAsync_MissingProject_ReturnsNotFoundWithEmptyBody()
    {
        var codeQueryService = Substitute.For<ICodeQueryService>();
        codeQueryService.QueryAsync(
            "question", 999, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromFailure(
                CodeCiir.Application.Projects.ProjectFailures.ProjectNotFound(999)));

        using var client = CreateClient(codeQueryService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries", UriKind.Relative),
            new { question = "question", project_id = 999 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    private HttpClient CreateClient(ICodeQueryService codeQueryService) => factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICodeQueryService>();
            services.AddScoped(_ => codeQueryService);
        }))
        .CreateClient();
}
