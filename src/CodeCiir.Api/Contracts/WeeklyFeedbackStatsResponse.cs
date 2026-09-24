using CodeCiir.Application.Feedback;

namespace CodeCiir.Api.Contracts;

/// <summary>Feedback effectiveness statistics for a single ISO calendar week (Monday-Sunday).</summary>
/// <param name="WeekStart">Monday of this ISO calendar week.</param>
/// <param name="WeekEnd">Sunday of this ISO calendar week.</param>
/// <param name="Projects">
/// Every registered project (or the single project matching <c>projectId</c>, if given), ordered
/// by <c>projectId</c>, with counts/percentages for feedback created within this week and within
/// the overall requested window. Zero-filled when a project had no feedback in this specific week.
/// </param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record WeeklyFeedbackStatsResponse(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    IReadOnlyList<ProjectFeedbackStatsResponse> Projects)
{
    public static WeeklyFeedbackStatsResponse From(WeeklyFeedbackStats week) => new(
        week.WeekStart,
        week.WeekEnd,
        week.Projects.Select(ProjectFeedbackStatsResponse.From).ToList());
}
#pragma warning restore SA1313
