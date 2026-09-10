using CodeCiir.Api.Problems;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodeCiir.Api.Filters;

/// <summary>
/// Formats any exception that escapes an action as the "500" Problem Details shape, including
/// the raised exception's type/message/stack trace. Deliberate, accepted trade-off for ease of
/// debugging in this API - mirrors code-rag-api's contract.
/// </summary>
public sealed class UnhandledExceptionFilter(ILogger<UnhandledExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;

        logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path);

        var problemDetails = new ServerErrorProblemDetails
        {
            Type = "https://httpstatuses.io/500",
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An unexpected error occurred while processing the request.",
            Instance = context.HttpContext.Request.Path,
            Exception = new ServerErrorProblemDetails.ExceptionDetails(
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message,
                exception.StackTrace),
        };

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            ContentTypes = { "application/problem+json" },
        };

        context.ExceptionHandled = true;
    }
}
