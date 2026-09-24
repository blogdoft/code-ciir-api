using CodeCiir.Api.Problems;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodeCiir.Api.Filters;

/// <summary>
/// Translates any model-binding/validation failure (malformed JSON, wrong types, unknown
/// properties, DataAnnotations violations on a request DTO) into the API's single
/// <c>400</c> <c>application/problem+json</c> shape, before the action - and therefore the
/// Application-layer use case - ever runs. Actions never build validation problem details
/// themselves.
/// </summary>
public sealed class ModelValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = ProblemResults.BadRequestValidation(context.ModelState, context.HttpContext.Request.Path);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Nothing to do after the action has run.
    }
}
