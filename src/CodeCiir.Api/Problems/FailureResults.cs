using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Api.Authentication;
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

        // 401/403/404/5xx carry no response body (nothing about why is leaked to the client); the
        // outcome is only recorded in the application log.
        if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound or >= 500)
        {
            LogFailure(failure, status, context);
            return new StatusCodeResult(status);
        }

        return ProblemResults.Build(status, ReasonPhrases.GetReasonPhrase(status), failure.Message, context.Request.Path);
    }

    private static void LogFailure(Failure failure, int status, HttpContext context)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(FailureResults).FullName!);

        if (status == StatusCodes.Status403Forbidden)
        {
            logger.LogWarning(
                "Request {Method} {Path} forbidden for {Username} ({FailureCode}): {FailureMessage}",
                context.Request.Method,
                context.Request.Path,
                context.User.GetUsername(),
                failure.Code,
                failure.Message);
            return;
        }

        logger.Log(
            status >= StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Information,
            "Request {Method} {Path} failed with {Status} ({FailureCode}): {FailureMessage}",
            context.Request.Method,
            context.Request.Path,
            status,
            failure.Code,
            failure.Message);
    }

    private static int ParseStatus(string code)
    {
        var separatorIndex = code.IndexOf('-', StringComparison.Ordinal);
        var statusPart = separatorIndex > 0 ? code[..separatorIndex] : code;
        return int.TryParse(statusPart, out var status) ? status : StatusCodes.Status400BadRequest;
    }
}
