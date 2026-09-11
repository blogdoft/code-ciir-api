namespace CodeCiir.Application.Feedback;

/// <summary>
/// Feedback effectiveness statistics for a time window, as a dense week × project grid. Every ISO
/// calendar week overlapping the window is present, ordered by <see cref="WeeklyFeedbackStats.WeekStart"/>.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record FeedbackStatsResult(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyList<WeeklyFeedbackStats> Weeks);
#pragma warning restore SA1313
