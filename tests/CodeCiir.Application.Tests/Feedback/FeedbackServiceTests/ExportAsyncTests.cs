using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using CodeCiir.Application.Tests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.Feedback.FeedbackServiceTests;

public sealed class ExportAsyncTests : BaseFeedbackServiceTests
{
    public ExportAsyncTests()
    {
        FeedbackRepository.ExportAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    [Fact]
    public async Task Should_UseStartOfCurrentMonthUntilNow_When_NoDatesAreGiven()
    {
        var before = DateTime.UtcNow;

        var result = await Sut.ExportAsync(null, null, null);

        var after = DateTime.UtcNow;
        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBeInRange(before, after);
        result.Value.StartDate.ShouldBe(new DateTime(before.Year, before.Month, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Should_DefaultEndDateToNow_When_OnlyStartDateIsGiven()
    {
        var start = Utc(2026, 1, 1);
        var before = DateTime.UtcNow;

        var result = await Sut.ExportAsync(start, null, null);

        var after = DateTime.UtcNow;
        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBeInRange(before, after);
    }

    [Fact]
    public async Task Should_DefaultStartDateToStartOfEndMonth_When_OnlyEndDateIsGiven()
    {
        var end = DateTime.UtcNow;

        var result = await Sut.ExportAsync(null, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBe(end);
        result.Value.StartDate.ShouldBe(new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Should_UseTheExactWindow_When_BothDatesAreGiven()
    {
        var start = Utc(2026, 1, 1);
        var end = Utc(2026, 6, 1);

        var result = await Sut.ExportAsync(start, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(end);
    }

    [Fact]
    public async Task Should_ReturnInvalidDateRangeFailure_When_EffectiveStartDateIsAfterEndDate()
    {
        var result = await Sut.ExportAsync(Utc(2026, 6, 1), Utc(2026, 1, 1), null);

        result.ShouldBeFailure(FeedbackFailures.InvalidDateRange());
    }

    [Fact]
    public async Task Should_ReturnWindowTooLargeFailure_When_WindowExceedsMaximum()
    {
        var start = Utc(2025, 1, 1);

        var result = await Sut.ExportAsync(start, start.AddDays(FeedbackService.MaxWindowDays + 1), null);

        result.ShouldBeFailure(FeedbackFailures.WindowTooLarge());
    }

    [Fact]
    public async Task Should_ReturnProjectNotFoundFailure_When_ProjectFilterDoesNotExist()
    {
        const long missingProjectId = 999;
        GivenProjectDoesNotExist(missingProjectId);

        var result = await Sut.ExportAsync(null, null, missingProjectId);

        result.ShouldBeFailure(ProjectFailures.ProjectNotFound(missingProjectId));
    }

    [Fact]
    public async Task Should_ReturnTheRepositoryRows_When_ProjectFilterExists()
    {
        const long projectId = 1;
        GivenProjectExists(projectId);
        var start = Utc(2026, 1, 1);
        var end = Utc(2026, 2, 1);
        List<FeedbackExportRow> rows =
        [
            new(1, projectId, Faker.Commerce.ProductName(), Faker.Lorem.Sentence(), true, [0.9], null, Agent, start.AddDays(1)),
        ];
        FeedbackRepository.ExportAsync(start, end, projectId, Arg.Any<CancellationToken>()).Returns(rows);

        var result = await Sut.ExportAsync(start, end, projectId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rows.ShouldBe(rows);
    }
}
