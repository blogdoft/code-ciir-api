using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;

namespace CodeCiir.Api.Problems;

/// <summary>Builds RFC 7807 "application/problem+json" results with the fields the OpenAPI contract expects.</summary>
public static class ProblemResults
{
    /// <summary>
    /// Builds a 400 response for a request ASP.NET's own model binding/JSON deserialization
    /// rejected before any controller action ran (malformed JSON, a value of the wrong type, an
    /// unmapped property rejected by <c>JsonUnmappedMemberHandling.Disallow</c>, a missing
    /// <c>JsonRequired</c> property, ...). This always names which field(s) were rejected and why, via the standard
    /// <c>errors</c> extension (<see cref="ValidationProblemDetails.Errors"/>) - the same shape
    /// ASP.NET's own automatic validation responses use - built from <paramref name="modelState"/>
    /// instead of a single generic sentence.
    /// </summary>
    /// <param name="modelState">The model binding state for the failed request, supplying one or more per-field errors.</param>
    /// <param name="instance">Request path to report as the problem's <c>instance</c>.</param>
    public static IActionResult BadRequestValidation(ModelStateDictionary modelState, PathString instance)
    {
        var problemDetails = new ValidationProblemDetails(ToCamelCaseErrors(modelState))
        {
            Type = $"https://httpstatuses.io/{StatusCodes.Status400BadRequest}",
            Title = "Bad Request",
            Status = StatusCodes.Status400BadRequest,
            Detail = "The request body is missing or invalid - see 'errors' for which field(s) and why.",
            Instance = instance,
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    }

    public static IActionResult Build(int status, string title, string detail, PathString instance)
    {
        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = instance,
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }

    // ModelState keys are C# member paths ("Question", "QualifiedName.Value") for validation-attribute
    // failures but JSON paths ("$.projectId") for deserialization failures. The API's wire format is
    // camelCase, so the first kind is renamed to match; JSON paths already are.
    private static Dictionary<string, string[]> ToCamelCaseErrors(ModelStateDictionary modelState) => modelState
        .Where(entry => entry.Value?.Errors.Count > 0)
        .ToDictionary(
            entry => ToCamelCasePath(entry.Key),
            entry => entry.Value!.Errors
                .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "The input was not valid." : error.ErrorMessage)
                .ToArray());

    private static string ToCamelCasePath(string key) => key.StartsWith('$')
        ? key
        : string.Join('.', key.Split('.').Select(segment => JsonNamingPolicy.CamelCase.ConvertName(segment)));
}
