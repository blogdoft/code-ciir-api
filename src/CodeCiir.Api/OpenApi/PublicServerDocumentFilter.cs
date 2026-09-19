using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodeCiir.Api.OpenApi;

/// <summary>
/// Sets the OpenAPI document's server URL to wherever this instance is actually reachable, so
/// Swagger UI's "Try it out" requests land on a real address whether the app is running behind
/// the cluster's blogdoft.home.arpa/code-brain ingress prefix (which strips "/code-brain" before
/// forwarding - see .eng/k8s/middleware.yaml - leaving nothing in the request itself that reveals
/// it) or directly via `dotnet run`. The "PublicBaseUrl" configuration value supplies that
/// external prefix explicitly for the one deployment that needs it; everywhere else, the current
/// request's own scheme/host stand in for it. Mirrors code-ciir-indexer's
/// PublicServerDocumentTransformer (the Microsoft.AspNetCore.OpenApi equivalent of this
/// Swashbuckle IDocumentFilter).
/// </summary>
internal sealed class PublicServerDocumentFilter(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        var baseUrl = configuration["PublicBaseUrl"] ?? (request is null ? null : $"{request.Scheme}://{request.Host}");

        if (baseUrl is not null)
        {
            swaggerDoc.Servers = [new OpenApiServer { Url = baseUrl }];
        }
    }
}
