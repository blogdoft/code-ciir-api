using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeCiir.Api.Tests.Controllers.CodeQueriesControllerTests;

public sealed class SubmitFeedbackAsyncTests(CustomWebApplicationFactory factory) : BaseCodeQueriesControllerTests(factory)
{
    private const string Agent = "claude code";

    [Fact]
    public async Task Should_ReturnCreatedWithoutLocationHeader_When_FeedbackIsValid()
    {
        var question = Faker.Lorem.Sentence();
        var expected = new FeedbackResult(Faker.Random.Long(1, 1000), 1, question, true, [0.8], null, Agent, DateTime.UtcNow);
        FeedbackService.SubmitAsync(1, question, true, Arg.Any<IReadOnlyList<double>>(), null, Agent, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromSuccess(expected));
        using var client = CreateClient();

        using var response = await PostAsync(client, FeedbackPath, new { projectId = 1, question, useful = true, similarities = new[] { 0.8 }, user = Agent });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Should_EchoThePersistedRecordInCamelCase_When_FeedbackIsValid()
    {
        var question = Faker.Lorem.Sentence();
        var expected = new FeedbackResult(Faker.Random.Long(1, 1000), 1, question, true, [0.8], null, Agent, DateTime.UtcNow);
        FeedbackService.SubmitAsync(1, question, true, Arg.Any<IReadOnlyList<double>>(), null, Agent, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromSuccess(expected));
        using var client = CreateClient();

        using var response = await PostAsync(client, FeedbackPath, new { projectId = 1, question, useful = true, similarities = new[] { 0.8 }, user = Agent });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        (body.GetProperty("id").GetInt64(), body.GetProperty("projectId").GetInt64(), body.TryGetProperty("createdAt", out _)).ShouldBe((expected.Id, 1L, true));
    }

    [Fact]
    public async Task Should_ReturnBadRequestNamingTheUserWithoutCallingTheService_When_UserIsMissing()
    {
        using var client = CreateClient();

        using var response = await PostAsync(client, FeedbackPath, new { projectId = 1, question = Faker.Lorem.Sentence(), useful = true, similarities = new[] { 0.8 } });

        await AssertBadRequestNamingAsync(response, "user");
        await FeedbackService.DidNotReceiveWithAnyArgs().SubmitAsync(default, default, default, default, default, default, default);
    }

    [Fact]
    public async Task Should_ReturnBadRequestNamingTheProjectId_When_ProjectIdIsMissing()
    {
        using var client = CreateClient();

        using var response = await PostAsync(client, FeedbackPath, new { question = Faker.Lorem.Sentence(), useful = true, similarities = new[] { 0.8 }, user = Agent });

        await AssertBadRequestNamingAsync(response, "projectId");
    }

    [Fact]
    public async Task Should_ReturnBadRequestNamingTheSimilarities_When_TooManyAreSent()
    {
        var similarities = Enumerable.Repeat(0.5, Application.Feedback.FeedbackService.MaxSimilaritiesCount + 1).ToArray();
        using var client = CreateClient();

        using var response = await PostAsync(client, FeedbackPath, new { projectId = 1, question = Faker.Lorem.Sentence(), useful = true, similarities, user = Agent });

        await AssertBadRequestNamingAsync(response, "similarities");
    }

    [Fact]
    public async Task Should_ReturnBadRequestNamingTheUseful_When_UsefulIsMissing()
    {
        using var client = CreateClient();

        using var response = await PostAsync(client, FeedbackPath, new { projectId = 1, question = Faker.Lorem.Sentence(), similarities = new[] { 0.8 }, user = Agent });

        await AssertBadRequestNamingAsync(response, "useful");
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithEmptyBody_When_ProjectDoesNotExist()
    {
        var question = Faker.Lorem.Sentence();
        FeedbackService.SubmitAsync(999, question, true, Arg.Any<IReadOnlyList<double>>(), null, Agent, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromFailure(ProjectFailures.ProjectNotFound(999)));
        using var client = CreateClient();

        using var response = await PostAsync(client, FeedbackPath, new { projectId = 999, question, useful = true, similarities = new[] { 0.8 }, user = Agent });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    private static async Task AssertBadRequestNamingAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.EnumerateObject().ShouldContain(error => error.Name.Contains(field, StringComparison.Ordinal));
    }
}
