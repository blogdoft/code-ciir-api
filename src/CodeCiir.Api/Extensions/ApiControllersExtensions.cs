using CodeCiir.Api.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeCiir.Api.Extensions;

public static class ApiControllersExtensions
{
    /// <summary>
    /// Registers MVC controllers with the API's cross-cutting behavior: JSON-only request bodies,
    /// camelCase <c>System.Text.Json</c> serialization, the global exception filter (bare 500, no
    /// leaked details) and the global model-validation filter (400 <c>application/problem+json</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                options.Filters.Add<UnhandledExceptionFilter>();
                options.Filters.Add<ModelValidationFilter>();

                // Request bodies are always application/json (per the OpenAPI contract) - restrict
                // content negotiation accordingly. There is no equivalent global filter for
                // responses: a global/controller-level [Produces] unconditionally overwrites
                // ObjectResult.ContentTypes - including the "application/problem+json" that
                // ProblemResults sets explicitly on error responses - silently downgrading them to
                // application/json (or 406, depending on the Accept header). Each action instead
                // declares its own 200 content type directly via ProducesResponseType, which only
                // affects that specific status code.
                options.Filters.Add(new ConsumesAttribute("application/json"));
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            // ModelValidationFilter (registered above) is the single place that turns a failed
            // binding/validation into a 400 Problem Details - it names which field(s) failed and
            // why. The built-in [ApiController] filter would race it with its own factory.
            options.SuppressModelStateInvalidFilter = true;

            // Without this, [ApiController] rewrites a bare NotFoundResult into a JSON Problem
            // Details body - the contract requires 404 responses to have no body at all.
            options.SuppressMapClientErrors = true;
        });

        // Request bodies are always plain application/json - the JSON input formatter otherwise
        // also advertises text/json and the application/*+json structured-syntax wildcard as
        // acceptable, which leaks into the generated OpenAPI document's requestBody content types.
        // The output formatter is deliberately left untouched: ProblemResults relies on its
        // application/*+json wildcard support to actually serve application/problem+json error
        // responses. PostConfigure runs after AddControllers has populated the formatter list,
        // regardless of registration order.
        services.PostConfigure<MvcOptions>(options =>
        {
            foreach (var supportedMediaTypes in options.InputFormatters.OfType<SystemTextJsonInputFormatter>()
                .Select(formatter => formatter.SupportedMediaTypes))
            {
                supportedMediaTypes.Clear();
                supportedMediaTypes.Add("application/json");
            }
        });

        return services;
    }
}
