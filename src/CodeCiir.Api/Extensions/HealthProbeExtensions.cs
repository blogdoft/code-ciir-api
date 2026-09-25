using Microsoft.AspNetCore.Http.Features;

namespace CodeCiir.Api.Extensions;

public static class HealthProbeExtensions
{
    /// <summary>
    /// Kubernetes liveness/readiness probe. Deliberately a terminal branch rather than an MVC
    /// endpoint, so the probes' frequent checks stay out of the authorization pipeline and never
    /// need a token even when Keycloak is enabled. Also opts out of the HTTP server metrics (a branch
    /// has no endpoint metadata to carry <c>DisableHttpMetrics</c>, so the metrics feature is flagged
    /// directly).
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    public static WebApplication MapHealthProbe(this WebApplication app)
    {
        app.Map(ObservabilityExtensions.HealthPath, healthApp => healthApp.Run(context =>
        {
            var metrics = context.Features.Get<IHttpMetricsTagsFeature>();
            if (metrics is not null)
            {
                metrics.MetricsDisabled = true;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }));

        return app;
    }
}
