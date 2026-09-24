using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Projects;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeCiir.Api.Tests.Controllers.CodeQueriesControllerTests;

public sealed class QueryAsyncTests(CustomWebApplicationFactory factory) : BaseCodeQueriesControllerTests(factory)
{
    private const string Question = "where is Baz?";

    private static readonly Guid ProjectId = Guid.NewGuid();

    [Fact]
    public async Task Should_ReturnOkWithCamelCaseMatchesAndRelations_When_QuestionIsValid()
    {
        GivenQueryReturns(ProjectId);
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question, projectId = ProjectId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var match = (await ReadBodyAsync(response)).GetProperty("matches")[0];
        match.GetProperty("symbolQualifiedName").GetString().ShouldBe("Foo.Bar.Baz");
        match.GetProperty("gitUrl").GetString().ShouldBe("https://github.com/acme/widgets");
        match.GetProperty("gitRawUrl").GetString().ShouldBe("https://raw.githubusercontent.com/acme/widgets/main/src/Baz.cs");
        var relation = match.GetProperty("relations")[0];
        (relation.GetProperty("relationType").GetString(), relation.GetProperty("targetSymbol").GetString()).ShouldBe(("calls", "Foo.Bar.Qux"));
    }

    [Fact]
    public async Task Should_ReturnTheRelationshipGraphInCamelCase_When_ProjectIdIsGiven()
    {
        GivenQueryReturns(ProjectId);
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question, projectId = ProjectId });

        var graph = (await ReadBodyAsync(response)).GetProperty("graph");
        graph.GetProperty("nodes").GetArrayLength().ShouldBe(1);
        graph.GetProperty("edges")[0].GetProperty("relationType").GetString().ShouldBe("calls");
        graph.GetProperty("truncated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_NotExposeAnySnakeCaseProperty_When_ResponseIsSerialized()
    {
        GivenQueryReturns(ProjectId);
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question, projectId = ProjectId });

        (await response.Content.ReadAsStringAsync()).ShouldNotContain("_");
    }

    [Fact]
    public async Task Should_SearchAcrossProjects_When_ProjectIdIsOmitted()
    {
        CodeQueryService.QueryAsync(Question, null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromSuccess(new CodeQueryResponse([], CodeGraph.Empty)));
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await CodeQueryService.Received(1).QueryAsync(Question, null, null, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("  ")]
    [InlineData("")]
    public async Task Should_ReturnBadRequestWithoutCallingTheService_When_QuestionIsBlank(string question)
    {
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question });

        await AssertBadRequestNamingAsync(response, "question");
        await CodeQueryService.DidNotReceiveWithAnyArgs().QueryAsync(default, default, default, default, default, default, default, default);
    }

    [Fact]
    public async Task Should_ReturnBadRequestNamingTheQuestion_When_QuestionIsMissing()
    {
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { projectId = ProjectId });

        await AssertBadRequestNamingAsync(response, "question");
    }

    [Fact]
    public async Task Should_ReturnBadRequestNamingTheQuestion_When_QuestionExceedsMaximumLength()
    {
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = new string('a', Application.CodeQueries.CodeQueryService.MaxQuestionLength + 1) });

        await AssertBadRequestNamingAsync(response, "question");
    }

    [Fact]
    public async Task Should_ReturnBadRequestNamingTheProjectId_When_ProjectIdIsNotAValidGuid()
    {
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question, projectId = "not-a-guid" });

        await AssertBadRequestNamingAsync(response, "projectId");
    }

    [Fact]
    public async Task Should_ReturnBadRequestNamingTheMinSimilarity_When_MinSimilarityIsOutOfRange()
    {
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question, minSimilarity = 1.5 });

        await AssertBadRequestNamingAsync(response, "minSimilarity");
    }

    [Fact]
    public async Task Should_ReturnBadRequestNamingTheField_When_ASnakeCaseFieldIsSent()
    {
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question, project_id = ProjectId });

        await AssertBadRequestNamingAsync(response, "project_id");
    }

    [Fact]
    public async Task Should_ReturnProblemDetailsWithTheFailureMessage_When_ServiceReportsAValidationFailure()
    {
        var failure = CodeQueryFailures.KindFilterValueRequired();
        CodeQueryService.QueryAsync(Question, null, null, "   ", null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromFailure(failure));
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question, kind = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await ReadBodyAsync(response)).GetProperty("detail").GetString().ShouldBe(failure.Message);
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithEmptyBody_When_ProjectDoesNotExist()
    {
        var missingProjectId = Guid.NewGuid();
        CodeQueryService.QueryAsync(Question, missingProjectId, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromFailure(ProjectFailures.ProjectNotFound(missingProjectId)));
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question, projectId = missingProjectId });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnInternalServerErrorWithEmptyBody_When_ServiceThrows()
    {
        CodeQueryService.QueryAsync(Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<double?>(), Arg.Any<string?>(), Arg.Any<QualifiedNameFilterOperator?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns<Result<CodeQueryResponse>>(_ => throw new InvalidOperationException("connection string leaked: Password=secret"));
        using var client = CreateClient();

        using var response = await PostAsync(client, CodeQueriesPath, new { question = Question });

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task AssertBadRequestNamingAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var errors = (await ReadBodyAsync(response)).GetProperty("errors");
        errors.EnumerateObject().ShouldContain(error => error.Name.Contains(field, StringComparison.Ordinal));
    }

    private void GivenQueryReturns(Guid projectId)
    {
        var match = new CodeQueryResult(
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
            with
        { Relations = [new MatchRelation(1, 2, "calls", "Foo.Bar.Qux", "project")] };
        var graph = new CodeGraph(
            [new GraphNode(2, "method", "Foo.Bar", "Qux", "Foo.Bar.Qux", null, "src/Qux.cs", 1)],
            [new GraphEdge(1, 2, "calls", "Foo.Bar.Qux", "project", 1)],
            false);
        CodeQueryService.QueryAsync(Question, projectId, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<CodeQueryResponse>.FromSuccess(new CodeQueryResponse([match], graph)));
    }
}
