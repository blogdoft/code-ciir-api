using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodeCiir.Api.Filters;

/// <summary>
/// Last-resort handler for exceptions escaping a controller action (bugs, infrastructure
/// outages). Replies with a bare <c>500</c> - no body - so nothing about the failure (exception
/// type, message, stack trace, connection strings, ...) leaks to the client; the full exception
/// goes to the application log instead.
/// </summary>
public sealed class UnhandledExceptionFilter(ILogger<UnhandledExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        logger.LogError(
            context.Exception,
            "Unhandled exception while processing {Method} {Path}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path);

        context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
        context.ExceptionHandled = true;
    }
}
