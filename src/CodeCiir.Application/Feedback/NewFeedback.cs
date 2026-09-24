namespace CodeCiir.Application.Feedback;

/// <summary>
/// A validated piece of feedback about to be recorded - everything <see cref="FeedbackResult"/>
/// has except the values the database generates (<c>Id</c>, <c>CreatedAt</c>).
/// </summary>
/// <param name="ProjectId">Id of the project the original code-queries call was scoped to.</param>
/// <param name="Question">The original natural language question.</param>
/// <param name="Useful">Whether the results of the original call were useful.</param>
/// <param name="Similarities">The similarity values the original call returned.</param>
/// <param name="Reason">Optional free-text explanation.</param>
/// <param name="User">Identity of the caller submitting the feedback.</param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record NewFeedback(
    long ProjectId,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string User);
#pragma warning restore SA1313
