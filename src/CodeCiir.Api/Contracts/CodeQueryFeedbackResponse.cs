using CodeCiir.Application.Feedback;

namespace CodeCiir.Api.Contracts;

/// <summary>
/// A recorded piece of feedback on a prior code query. Serializes as camelCase. There is no
/// GET endpoint for retrieving it later - this response is the only view of it.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryFeedbackResponse(
    long Id,
    Guid ProjectId,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string User,
    DateTime CreatedAt)
{
    /// <summary>Maps a persisted feedback record to its response shape.</summary>
    /// <param name="result">The persisted feedback record.</param>
    /// <param name="projectId">
    /// The caller-supplied project public id the feedback was submitted for - echoed back rather
    /// than round-tripped through <paramref name="result"/>, which only carries the internal id.
    /// </param>
    public static CodeQueryFeedbackResponse From(FeedbackResult result, Guid projectId) => new(
        result.Id,
        projectId,
        result.Question,
        result.Useful,
        result.Similarities,
        result.Reason,
        result.User,
        result.CreatedAt);
}
#pragma warning restore SA1313
