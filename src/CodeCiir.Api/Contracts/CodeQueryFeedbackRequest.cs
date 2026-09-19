using System.Text.Json.Serialization;

namespace CodeCiir.Api.Contracts;

/// <summary>Feedback on a prior `POST .../code-queries` call, scoped to a single project.</summary>
/// <param name="ProjectId">
/// Identifier of the project the original code-queries call was scoped to, corresponding to the
/// id of a project returned by the list_projects MCP tool. Required, must be a positive 64-bit
/// integer.
/// </param>
/// <param name="Question">The original natural language question, echoed back on the persisted record.</param>
/// <param name="Useful">Whether the results of the original code-queries call were useful.</param>
/// <param name="Similarities">The similarity values the original code-queries call returned.</param>
/// <param name="Reason">Optional free-text explanation for why the results were/weren't useful.</param>
/// <param name="User">Identity of the caller submitting the feedback.</param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CodeQueryFeedbackRequest(
    long? ProjectId,
    string? Question,
    bool? Useful,
    IReadOnlyList<double>? Similarities,
    string? Reason,
    string? User);
#pragma warning restore SA1313
