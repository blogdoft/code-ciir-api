using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using CodeCiir.Application.Tests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.Feedback.FeedbackServiceTests;

public sealed class GetStatsAsyncTests : BaseFeedbackServiceTests
{
    public GetStatsAsyncTests()
    {
        FeedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    [Fact]
    public async Task Should_UseTheLast30DaysEndingNow_When_NoDatesAreGiven()
    {
        var before = DateTime.UtcNow;

        var result = await Sut.GetStatsAsync(null, null, null);

        var after = DateTime.UtcNow;
        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBeInRange(before, after);
        (result.Value.EndDate - result.Value.StartDate).ShouldBe(TimeSpan.FromDays(FeedbackService.DefaultWindowDays));
    }

    [Fact]
    public async Task Should_DeriveEndDateAs30DaysAfterStart_When_OnlyStartDateIsGiven()
    {
        var start = Utc(2026, 1, 1);

        var result = await Sut.GetStatsAsync(start, null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(start.AddDays(FeedbackService.DefaultWindowDays));
    }

    [Fact]
    public async Task Should_DeriveStartDateAs30DaysBeforeEnd_When_OnlyEndDateIsGiven()
    {
        var end = Utc(2026, 1, 31);

        var result = await Sut.GetStatsAsync(null, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBe(end);
        result.Value.StartDate.ShouldBe(end.AddDays(-FeedbackService.DefaultWindowDays));
    }

    [Fact]
    public async Task Should_UseTheExactWindow_When_BothDatesAreGiven()
    {
        var start = Utc(2026, 1, 1);
        var end = Utc(2026, 6, 1);

        var result = await Sut.GetStatsAsync(start, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(end);
    }

    [Fact]
    public async Task Should_ReturnInvalidDateRangeFailure_When_StartDateIsAfterEndDate()
    {
        var result = await Sut.GetStatsAsync(Utc(2026, 6, 1), Utc(2026, 1, 1), null);

        result.ShouldBeFailure(FeedbackFailures.InvalidDateRange());
    }

    [Fact]
    public async Task Should_ReturnWindowTooLargeFailure_When_WindowExceedsMaximum()
    {
        var start = Utc(2025, 1, 1);

        var result = await Sut.GetStatsAsync(start, start.AddDays(FeedbackService.MaxWindowDays + 1), null);

        result.ShouldBeFailure(FeedbackFailures.WindowTooLarge());
    }

    [Fact]
    public async Task Should_ReturnProjectNotFoundFailure_When_ProjectFilterDoesNotExist()
    {
        var missingProjectId = Guid.NewGuid();
        GivenProjectDoesNotExist(missingProjectId);

        var result = await Sut.GetStatsAsync(null, null, missingProjectId);

        result.ShouldBeFailure(ProjectFailures.ProjectNotFound(missingProjectId));
    }

    [Fact]
    public async Task Should_ReturnTheRepositoryWeeks_When_ProjectFilterExists()
    {
        const long internalProjectId = 1;
        var projectId = Guid.NewGuid();
        GivenProjectExists(projectId, internalProjectId);
        List<WeeklyFeedbackStats> weeks =
        [
            new(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 9), [new ProjectFeedbackStats(projectId, Faker.Commerce.ProductName(), 4, 3, 1, 75, 25)]),
        ];
        FeedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), internalProjectId, Arg.Any<CancellationToken>())
            .Returns(weeks);

        var result = await Sut.GetStatsAsync(null, null, projectId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Weeks.ShouldBe(weeks);
    }
}
