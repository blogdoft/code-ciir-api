using BlogDoFT.Libs.ResultPattern;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CodeCiir.Api.Problems;

/// <summary>
/// Translates a domain <see cref="Failure"/> into an HTTP result. Application services encode
/// the intended HTTP status as the leading digits of <see cref="Failure.Code"/> (e.g.
/// "400-name-filter-empty", "404-project-not-found"), so this stays a single, generic mapping
/// instead of a per-endpoint switch statement.
/// </summary>
public static class FailureResults
{
    public static IActionResult ToActionResult(this Failure failure, HttpContext context)
    {
        var status = ParseStatus(failure.Code);

        if (status == StatusCodes.Status404NotFound)
        {
            return new NotFoundResult();
        }

        return ProblemResults.Build(status, ReasonPhrases.GetReasonPhrase(status), failure.Message, context.Request.Path);
    }

    private static int ParseStatus(string code)
    {
        var separatorIndex = code.IndexOf('-', StringComparison.Ordinal);
        var statusPart = separatorIndex > 0 ? code[..separatorIndex] : code;
        return int.TryParse(statusPart, out var status) ? status : StatusCodes.Status400BadRequest;
    }
}
