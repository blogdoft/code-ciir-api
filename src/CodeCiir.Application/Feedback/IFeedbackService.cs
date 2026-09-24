using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Feedback;

public interface IFeedbackService
{
    Task<Result<FeedbackResult>> SubmitAsync(
        Guid projectId,
        string? question,
        bool? useful,
        IReadOnlyList<double>? similarities,
        string? reason,
        string? user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns feedback effectiveness statistics for a time window, as a dense week × project
    /// grid - see .specs/06-code-query-feedback.md for the full default-window and validation
    /// rules.
    /// </summary>
    /// <param name="startDate">
    /// Inclusive lower bound (UTC) of the window. <c>null</c> is treated per the default-window
    /// rules together with <paramref name="endDate"/>.
    /// </param>
    /// <param name="endDate">
    /// Inclusive upper bound (UTC) of the window. <c>null</c> is treated per the default-window
    /// rules together with <paramref name="startDate"/>.
    /// </param>
    /// <param name="projectId">
    /// When given, restricts every week's project list to this single project; a non-existent
    /// project results in a 404 failure.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    Task<Result<FeedbackStatsResult>> GetStatsAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every raw feedback record for a time window, as a flat list ordered by
    /// <c>created_at</c> ascending - see .specs/06-code-query-feedback.md for the full
    /// default-window and validation rules.
    /// </summary>
    /// <param name="startDate">
    /// Inclusive lower bound (UTC) of the window. <c>null</c> defaults to the first day of the
    /// current UTC month, independently of <paramref name="endDate"/>.
    /// </param>
    /// <param name="endDate">
    /// Inclusive upper bound (UTC) of the window. <c>null</c> defaults to now (UTC),
    /// independently of <paramref name="startDate"/>.
    /// </param>
    /// <param name="projectId">
    /// When given, restricts the export to this single project; a non-existent project results
    /// in a 404 failure.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    Task<Result<FeedbackExportResult>> ExportAsync(
        DateTime? startDate,
        DateTime? endDate,
        Guid? projectId,
        CancellationToken cancellationToken = default);
}
