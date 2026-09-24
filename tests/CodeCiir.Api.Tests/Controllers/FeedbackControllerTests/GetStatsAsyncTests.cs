using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeCiir.Api.Tests.Controllers.FeedbackControllerTests;

public sealed class GetStatsAsyncTests(CustomWebApplicationFactory factory) : BaseFeedbackControllerTests(factory)
{
    private const string StatsPath = "/api/code-queries/feedback/stats";

    [Fact]
    public async Task Should_ReturnADenseWeeklyGridInCamelCase_When_RequestIsValid()
    {
        var start = Utc(2026, 8, 4);
        var end = Utc(2026, 9, 3);
        var projectId = Guid.NewGuid();
        var projectName = Faker.Commerce.ProductName();
        var stats = new FeedbackStatsResult(
            start,
            end,
            [new WeeklyFeedbackStats(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 9), [new ProjectFeedbackStats(projectId, projectName, 5, 4, 1, 80, 20)])]);
        FeedbackService.GetStatsAsync(start, end, projectId, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromSuccess(stats));
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{StatsPath}?startDate=2026-08-04T00:00:00Z&endDate=2026-09-03T00:00:00Z&projectId={projectId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var week = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("weeks")[0];
        week.GetProperty("weekStart").GetString().ShouldBe("2026-08-03");
        var project = week.GetProperty("projects")[0];
        (project.GetProperty("projectId").GetGuid(), project.GetProperty("usefulPercentage").GetDouble()).ShouldBe((projectId, 80d));
    }

    [Fact]
    public async Task Should_CallTheServiceWithNulls_When_NoQueryParameterIsGiven()
    {
        FeedbackService.GetStatsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromSuccess(new FeedbackStatsResult(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, [])));
        using var client = CreateClient();

        using var response = await GetAsync(client, StatsPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await FeedbackService.Received(1).GetStatsAsync(null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnBadRequestAsProblemDetails_When_StartDateIsAfterEndDate()
    {
        var failure = FeedbackFailures.InvalidDateRange();
        FeedbackService.GetStatsAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromFailure(failure));
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{StatsPath}?startDate=2026-06-01T00:00:00Z&endDate=2026-01-01T00:00:00Z");

        await AssertProblemAsync(response, failure);
    }

    [Fact]
    public async Task Should_ReturnBadRequestAsProblemDetails_When_WindowIsTooLarge()
    {
        var failure = FeedbackFailures.WindowTooLarge();
        FeedbackService.GetStatsAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromFailure(failure));
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{StatsPath}?startDate=2020-01-01T00:00:00Z&endDate=2022-01-01T00:00:00Z");

        await AssertProblemAsync(response, failure);
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithEmptyBody_When_ProjectDoesNotExist()
    {
        var missingProjectId = Guid.NewGuid();
        FeedbackService.GetStatsAsync(null, null, missingProjectId, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackStatsResult>.FromFailure(ProjectFailures.ProjectNotFound(missingProjectId)));
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{StatsPath}?projectId={missingProjectId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnInternalServerErrorWithEmptyBody_When_ServiceThrows()
    {
        FeedbackService.GetStatsAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns<Result<FeedbackStatsResult>>(_ => throw new InvalidOperationException("boom"));
        using var client = CreateClient();

        using var response = await GetAsync(client, StatsPath);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, Failure failure)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString().ShouldBe(failure.Message);
    }
}
