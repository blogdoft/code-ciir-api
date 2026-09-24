namespace CodeCiir.Api.Extensions;

public static class HealthProbeExtensions
{
    /// <summary>
    /// Kubernetes liveness/readiness probe. Deliberately a terminal branch rather than an MVC
    /// endpoint, so the probes' frequent checks stay out of the authorization pipeline and never
    /// need a token even when Keycloak is enabled.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    public static WebApplication MapHealthProbe(this WebApplication app)
    {
        app.Map("/health", healthApp => healthApp.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }));

        return app;
    }
}
