using Microsoft.AspNetCore.Mvc;

namespace CodeCiir.Api.Problems;

/// <summary>Parses a route parameter that must be a positive 64-bit integer id (e.g. <c>projectId</c>).</summary>
public static class RouteId
{
    public static bool TryParsePositive(
        string value,
        string parameterName,
        PathString requestPath,
        out long id,
        out IActionResult? problem)
    {
        if (long.TryParse(value, out id) && id >= 1)
        {
            problem = null;
            return true;
        }

        problem = ProblemResults.BadRequest(
            $"The '{parameterName}' route parameter must be a positive integer; received '{value}'.",
            requestPath);
        return false;
    }
}
