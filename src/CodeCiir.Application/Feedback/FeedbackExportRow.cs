namespace CodeCiir.Application.Feedback;

/// <summary>A single raw feedback record, joined with its project's name, for CSV export.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record FeedbackExportRow(
    long Id,
    Guid ProjectId,
    string ProjectName,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string Username,
    DateTime CreatedAt);
#pragma warning restore SA1313
