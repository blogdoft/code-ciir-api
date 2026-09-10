namespace CodeCiir.Api.Contracts;

/// <summary>
/// A recorded piece of feedback on a prior code query. Serializes as snake_case. There is no
/// GET endpoint for retrieving it later - this response is the only view of it.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryFeedbackResponse(
    long Id,
    long ProjectId,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string User,
    DateTime CreatedAt);
#pragma warning restore SA1313
