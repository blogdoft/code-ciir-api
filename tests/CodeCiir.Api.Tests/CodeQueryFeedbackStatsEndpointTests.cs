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

public sealed class CodeQueryFeedbackStatsEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task GetStatsAsync_Valid_ReturnsOkWithDenseWeeklyGrid()
    {
        var start = new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);
        var stats = new FeedbackStatsResult(
            start,
            end,
            [new WeeklyFeedbackStats(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 9), [new ProjectFeedbackStats(1, "proj", 5, 4, 1, 80, 20)])]);
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.GetStatsAsync(start, end, 1, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromSuccess(stats));

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(
            new Uri("/api/v1/code-queries/feedback/stats?start_date=2026-08-04T00:00:00Z&end_date=2026-09-03T00:00:00Z&project_id=1", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var weeks = body.GetProperty("weeks");
        weeks.GetArrayLength().ShouldBe(1);
        var projects = weeks[0].GetProperty("projects");
        projects[0].GetProperty("project_id").GetInt64().ShouldBe(1);
        projects[0].GetProperty("useful_percentage").GetDouble().ShouldBe(80);
    }

    [Fact]
    public async Task GetStatsAsync_NoQueryParams_CallsServiceWithNullsAndDefaultProject()
    {
        var stats = new FeedbackStatsResult(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, []);
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.GetStatsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromSuccess(stats));

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(new Uri("/api/v1/code-queries/feedback/stats", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStatsAsync_InvalidDateRange_ReturnsBadRequest()
    {
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.GetStatsAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromFailure(FeedbackFailures.InvalidDateRange()));

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(
            new Uri("/api/v1/code-queries/feedback/stats?start_date=2026-06-01T00:00:00Z&end_date=2026-01-01T00:00:00Z", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetStatsAsync_WindowTooLarge_ReturnsBadRequest()
    {
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.GetStatsAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromFailure(FeedbackFailures.WindowTooLarge()));

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(
            new Uri("/api/v1/code-queries/feedback/stats?start_date=2020-01-01T00:00:00Z&end_date=2022-01-01T00:00:00Z", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetStatsAsync_ProjectNotFound_ReturnsNotFound()
    {
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.GetStatsAsync(null, null, 999, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromFailure(
                CodeCiir.Application.Projects.ProjectFailures.ProjectNotFound(999)));

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(new Uri("/api/v1/code-queries/feedback/stats?project_id=999", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsByteArrayAsync();
        content.ShouldBeEmpty();
    }

    private HttpClient CreateClient(IFeedbackService feedbackService) => factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFeedbackService>();
            services.AddScoped(_ => feedbackService);
        }))
        .CreateClient();
}
