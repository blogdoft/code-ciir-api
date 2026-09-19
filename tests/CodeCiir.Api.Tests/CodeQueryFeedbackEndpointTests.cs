using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Feedback;
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

public sealed class CodeQueryFeedbackEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task SubmitFeedbackAsync_Valid_ReturnsCreatedWithoutLocationHeader()
    {
        var expected = new FeedbackResult(1, 1, "question", true, [0.8], null, "claude code", DateTime.UtcNow);
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.SubmitAsync(1, "question", true, Arg.Any<IReadOnlyList<double>>(), null, "claude code", Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromSuccess(expected));

        using var client = CreateClient(feedbackService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries/feedback", UriKind.Relative),
            new { project_id = 1, question = "question", useful = true, similarities = new[] { 0.8 }, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_MissingUser_ReturnsBadRequest()
    {
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.SubmitAsync(1, "question", true, Arg.Any<IReadOnlyList<double>>(), null, null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromFailure(FeedbackFailures.UserRequired()));

        using var client = CreateClient(feedbackService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries/feedback", UriKind.Relative),
            new { project_id = 1, question = "question", useful = true, similarities = new[] { 0.8 } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_MissingProjectId_ReturnsBadRequest()
    {
        var feedbackService = Substitute.For<IFeedbackService>();

        using var client = CreateClient(feedbackService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries/feedback", UriKind.Relative),
            new { question = "question", useful = true, similarities = new[] { 0.8 }, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_MissingProject_ReturnsNotFound()
    {
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.SubmitAsync(999, "question", true, Arg.Any<IReadOnlyList<double>>(), null, "claude code", Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromFailure(
                CodeCiir.Application.Projects.ProjectFailures.ProjectNotFound(999)));

        using var client = CreateClient(feedbackService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries/feedback", UriKind.Relative),
            new { project_id = 999, question = "question", useful = true, similarities = new[] { 0.8 }, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private HttpClient CreateClient(IFeedbackService feedbackService) => factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFeedbackService>();
            services.AddScoped(_ => feedbackService);
        }))
        .CreateClient();
}
