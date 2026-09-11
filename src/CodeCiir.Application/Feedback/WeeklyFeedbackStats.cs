namespace CodeCiir.Application.Feedback;

/// <summary>
/// Feedback effectiveness statistics for a single ISO calendar week (Monday-Sunday), broken down
/// by project. Every registered project (or a single filtered project) is always present, even
/// with zero feedback in this specific week.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record WeeklyFeedbackStats(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    IReadOnlyList<ProjectFeedbackStats> Projects);
#pragma warning restore SA1313
