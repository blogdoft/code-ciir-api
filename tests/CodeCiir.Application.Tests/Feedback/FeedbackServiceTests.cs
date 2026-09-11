using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.Feedback;

public sealed class FeedbackServiceTests
{
    private static readonly Project TestProject = new(
        1,
        "proj",
        "bge-m3",
        1024,
        new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
        new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"),
        DateTime.UtcNow,
        DateTime.UtcNow);

    private readonly IProjectsRepository _projectsRepository = Substitute.For<IProjectsRepository>();
    private readonly IFeedbackRepository _feedbackRepository = Substitute.For<IFeedbackRepository>();
    private readonly FeedbackService _sut;

    public FeedbackServiceTests()
    {
        _sut = new FeedbackService(_projectsRepository, _feedbackRepository);
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestProject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SubmitAsync_MissingQuestion_ReturnsQuestionRequired(string? question)
    {
        var result = await _sut.SubmitAsync(1, question, true, [], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-question-required");
    }

    [Fact]
    public async Task SubmitAsync_MissingUseful_ReturnsUsefulRequired()
    {
        var result = await _sut.SubmitAsync(1, "question", null, [], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-useful-required");
    }

    [Fact]
    public async Task SubmitAsync_MissingSimilarities_ReturnsSimilaritiesRequired()
    {
        var result = await _sut.SubmitAsync(1, "question", true, null, null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-similarities-required");
    }

    [Fact]
    public async Task SubmitAsync_EmptySimilarities_IsValid()
    {
        _feedbackRepository.InsertAsync(1, "question", true, Arg.Any<IReadOnlyList<double>>(), null, "claude code", Arg.Any<CancellationToken>())
            .Returns(new FeedbackResult(1, 1, "question", true, [], null, "claude code", DateTime.UtcNow));

        var result = await _sut.SubmitAsync(1, "question", true, [], null, "claude code");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SubmitAsync_TooManySimilarities_ReturnsTooManySimilarities()
    {
        var similarities = Enumerable.Repeat(0.5, FeedbackService.MaxSimilaritiesCount + 1).ToList();

        var result = await _sut.SubmitAsync(1, "question", true, similarities, null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-too-many-similarities");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SubmitAsync_MissingUser_ReturnsUserRequired(string? user)
    {
        var result = await _sut.SubmitAsync(1, "question", true, [], null, user);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-user-required");
    }

    [Fact]
    public async Task SubmitAsync_ReasonTooLong_ReturnsReasonTooLong()
    {
        var tooLong = new string('a', FeedbackService.MaxReasonLength + 1);

        var result = await _sut.SubmitAsync(1, "question", false, [0.3], tooLong, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-reason-too-long");
    }

    [Fact]
    public async Task SubmitAsync_MissingProject_ReturnsProjectNotFound()
    {
        _projectsRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.SubmitAsync(999, "question", true, [], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("404-project-not-found");
    }

    [Fact]
    public async Task SubmitAsync_Valid_InsertsAndReturnsResult()
    {
        var expected = new FeedbackResult(1, 1, "question", true, [0.8, 0.6], null, "claude code", DateTime.UtcNow);
        _feedbackRepository.InsertAsync(1, "question", true, Arg.Any<IReadOnlyList<double>>(), null, "claude code", Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.SubmitAsync(1, "question", true, [0.8, 0.6], null, "claude code");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetStatsAsync_NoDatesGiven_UsesLast30Days()
    {
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var before = DateTime.UtcNow;
        var result = await _sut.GetStatsAsync(null, null, null);
        var after = DateTime.UtcNow;

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBeInRange(before, after);
        (result.Value.EndDate - result.Value.StartDate).ShouldBe(TimeSpan.FromDays(FeedbackService.DefaultWindowDays));
    }

    [Fact]
    public async Task GetStatsAsync_OnlyStartDateGiven_DerivesEndDate()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.GetStatsAsync(start, null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(start.AddDays(FeedbackService.DefaultWindowDays));
    }

    [Fact]
    public async Task GetStatsAsync_OnlyEndDateGiven_DerivesStartDate()
    {
        var end = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.GetStatsAsync(null, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBe(end);
        result.Value.StartDate.ShouldBe(end.AddDays(-FeedbackService.DefaultWindowDays));
    }

    [Fact]
    public async Task GetStatsAsync_BothDatesGiven_UsesExactWindow()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.GetStatsAsync(start, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(end);
    }

    [Fact]
    public async Task GetStatsAsync_StartDateAfterEndDate_ReturnsInvalidDateRange()
    {
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await _sut.GetStatsAsync(start, end, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-invalid-date-range");
    }

    [Fact]
    public async Task GetStatsAsync_WindowExceedsMaximum_ReturnsWindowTooLarge()
    {
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(FeedbackService.MaxWindowDays + 1);

        var result = await _sut.GetStatsAsync(start, end, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-window-too-large");
    }

    [Fact]
    public async Task GetStatsAsync_ProjectIdFilterDoesNotExist_ReturnsProjectNotFound()
    {
        _projectsRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.GetStatsAsync(null, null, 999);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("404-project-not-found");
    }

    [Fact]
    public async Task GetStatsAsync_ProjectIdFilterExists_ReturnsStats()
    {
        var weeks = new List<WeeklyFeedbackStats>
        {
            new(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 9), [new ProjectFeedbackStats(1, "proj", 4, 3, 1, 75, 25)]),
        };
        _feedbackRepository.GetStatsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), 1, Arg.Any<CancellationToken>())
            .Returns(weeks);

        var result = await _sut.GetStatsAsync(null, null, 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Weeks.ShouldBe(weeks);
    }

    [Fact]
    public async Task ExportAsync_NoDatesGiven_UsesStartOfCurrentMonthToNow()
    {
        _feedbackRepository.ExportAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var before = DateTime.UtcNow;
        var result = await _sut.ExportAsync(null, null, null);
        var after = DateTime.UtcNow;

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBeInRange(before, after);
        result.Value.StartDate.ShouldBe(new DateTime(before.Year, before.Month, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ExportAsync_OnlyStartDateGiven_DefaultsEndDateToNow()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.ExportAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var before = DateTime.UtcNow;
        var result = await _sut.ExportAsync(start, null, null);
        var after = DateTime.UtcNow;

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBeInRange(before, after);
    }

    [Fact]
    public async Task ExportAsync_OnlyEndDateGiven_DefaultsStartDateToStartOfCurrentMonth()
    {
        var end = DateTime.UtcNow;
        _feedbackRepository.ExportAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.ExportAsync(null, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.EndDate.ShouldBe(end);
        result.Value.StartDate.ShouldBe(new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ExportAsync_BothDatesGiven_UsesExactWindow()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        _feedbackRepository.ExportAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.ExportAsync(start, end, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.StartDate.ShouldBe(start);
        result.Value.EndDate.ShouldBe(end);
    }

    [Fact]
    public async Task ExportAsync_EffectiveStartDateAfterEndDate_ReturnsInvalidDateRange()
    {
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await _sut.ExportAsync(start, end, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-invalid-date-range");
    }

    [Fact]
    public async Task ExportAsync_WindowExceedsMaximum_ReturnsWindowTooLarge()
    {
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(FeedbackService.MaxWindowDays + 1);

        var result = await _sut.ExportAsync(start, end, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-window-too-large");
    }

    [Fact]
    public async Task ExportAsync_ProjectIdFilterDoesNotExist_ReturnsProjectNotFound()
    {
        _projectsRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.ExportAsync(null, null, 999);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("404-project-not-found");
    }

    [Fact]
    public async Task ExportAsync_ProjectIdFilterExists_ReturnsRows()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = new List<FeedbackExportRow>
        {
            new(1, 1, "proj", "question", true, [0.9], null, "claude code", start.AddDays(1)),
        };
        _feedbackRepository.ExportAsync(start, end, 1, Arg.Any<CancellationToken>()).Returns(rows);

        var result = await _sut.ExportAsync(start, end, 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rows.ShouldBe(rows);
    }
}
