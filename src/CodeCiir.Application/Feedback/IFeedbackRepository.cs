namespace CodeCiir.Application.Feedback;

public interface IFeedbackRepository
{
    Task<FeedbackResult> InsertAsync(
        long projectId,
        string question,
        bool useful,
        IReadOnlyList<double> similarities,
        string? reason,
        string user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns feedback effectiveness statistics as a dense week × project grid: every ISO
    /// calendar week overlapping <paramref name="startDate"/>/<paramref name="endDate"/> is
    /// present, and within each week, every eligible project is present (even with zero feedback
    /// in that specific week).
    /// </summary>
    /// <param name="startDate">Inclusive lower bound (UTC) of the window.</param>
    /// <param name="endDate">Inclusive upper bound (UTC) of the window.</param>
    /// <param name="projectId">When given, restricts every week's project list to this single project.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    Task<IReadOnlyList<WeeklyFeedbackStats>> GetStatsAsync(
        DateTime startDate,
        DateTime endDate,
        long? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every raw feedback record whose <c>created_at</c> falls within
    /// <paramref name="startDate"/>/<paramref name="endDate"/>, joined with its project's name,
    /// ordered by <c>created_at</c> ascending.
    /// </summary>
    /// <param name="startDate">Inclusive lower bound (UTC) of the window.</param>
    /// <param name="endDate">Inclusive upper bound (UTC) of the window.</param>
    /// <param name="projectId">When given, restricts the export to this single project.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    Task<IReadOnlyList<FeedbackExportRow>> ExportAsync(
        DateTime startDate,
        DateTime endDate,
        long? projectId,
        CancellationToken cancellationToken = default);
}
