namespace CodeCiir.Application.Feedback;

/// <summary>
/// A recorded piece of feedback on a prior code-queries call. Lives in its own table
/// (code_query_feedback) that code-ciir-api owns additively inside code3rag - see
/// .specs/06-code-query-feedback.md for why this is the one table this API is allowed to write.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record FeedbackResult(
    long Id,
    long ProjectId,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string User,
    DateTime CreatedAt);
#pragma warning restore SA1313
