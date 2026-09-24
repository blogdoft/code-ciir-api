using CodeCiir.Application.Feedback;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CodeCiir.Api.Contracts;

/// <summary>Feedback on a prior `POST .../code-queries` call, scoped to a single project.</summary>
/// <param name="ProjectId">
/// Identifier of the project the original code-queries call was scoped to, corresponding to the
/// id of a project returned by the list_projects MCP tool. Required.
/// </param>
/// <param name="Question">The original natural language question, echoed back on the persisted record.</param>
/// <param name="Useful">Whether the results of the original code-queries call were useful.</param>
/// <param name="Similarities">The similarity values the original code-queries call returned.</param>
/// <param name="Reason">Optional free-text explanation for why the results were/weren't useful.</param>
/// <param name="User">Identity of the caller submitting the feedback.</param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CodeQueryFeedbackRequest(
    [Required(ErrorMessage = "The 'projectId' field is required.")]
    Guid? ProjectId,
    [Required(AllowEmptyStrings = false, ErrorMessage = "The 'question' field is required and must not be empty or blank."),
        StringLength(FeedbackService.MaxQuestionLength, ErrorMessage = "The 'question' field must not exceed {1} characters.")]
    string? Question,
    [Required(ErrorMessage = "The 'useful' field is required.")] bool? Useful,
    [Required(ErrorMessage = "The 'similarities' field is required (may be an empty array)."),
        MaxLength(FeedbackService.MaxSimilaritiesCount, ErrorMessage = "The 'similarities' field must not contain more than {1} values.")]
    double[]? Similarities,
    [StringLength(FeedbackService.MaxReasonLength, ErrorMessage = "The 'reason' field must not exceed {1} characters.")] string? Reason,
    [Required(AllowEmptyStrings = false, ErrorMessage = "The 'user' field is required and must not be empty. For MCP callers, this must be the calling agent/tool's own name."),
        StringLength(FeedbackService.MaxUserLength, ErrorMessage = "The 'user' field must not exceed {1} characters.")]
    string? User);
#pragma warning restore SA1313
