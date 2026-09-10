using System.Text.Json.Serialization;

namespace CodeCiir.Api.Contracts;

/// <summary>Feedback on a prior `POST .../code-queries` call, scoped to a single project.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CodeQueryFeedbackRequest(
    string? Question,
    bool? Useful,
    IReadOnlyList<double>? Similarities,
    string? Reason,
    string? User);
#pragma warning restore SA1313
