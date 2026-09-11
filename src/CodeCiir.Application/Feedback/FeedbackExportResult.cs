namespace CodeCiir.Application.Feedback;

/// <summary>
/// Raw feedback records for a time window, as a flat list ordered by <c>created_at</c> ascending.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record FeedbackExportResult(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyList<FeedbackExportRow> Rows);
#pragma warning restore SA1313
