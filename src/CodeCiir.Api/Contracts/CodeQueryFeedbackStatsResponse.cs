using CodeCiir.Application.Feedback;

namespace CodeCiir.Api.Contracts;

/// <summary>
/// Feedback effectiveness statistics for a time window, as a dense week × project grid.
/// Serializes as camelCase.
/// </summary>
/// <param name="StartDate">Effective inclusive lower bound (UTC) of the aggregated window.</param>
/// <param name="EndDate">Effective inclusive upper bound (UTC) of the aggregated window.</param>
/// <param name="Weeks">
/// Every ISO calendar week (Monday-Sunday) overlapping the effective window, ordered by
/// <c>weekStart</c> ascending. Always includes every overlapping week, even ones with zero
/// feedback across all projects.
/// </param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryFeedbackStatsResponse(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyList<WeeklyFeedbackStatsResponse> Weeks)
{
    public static CodeQueryFeedbackStatsResponse From(FeedbackStatsResult result) => new(
        result.StartDate,
        result.EndDate,
        result.Weeks.Select(WeeklyFeedbackStatsResponse.From).ToList());
}
#pragma warning restore SA1313
