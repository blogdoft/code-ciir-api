using BlogDoFT.Libs.Api.OpenTelemetry.Extensions;
using Npgsql;
using OpenTelemetry.Trace;

namespace CodeCiir.Api.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Wires logging, metrics and tracing through <c>BlogDoFT.Libs.Api.OpenTelemetry</c>, driven by
    /// the <c>Observability</c> configuration section (exporters, OTLP endpoint - see
    /// appsettings.json and .eng/k8s/configmap.yaml). Console logs are structured JSON (message
    /// template properties and scopes - trace/span ids included - become fields).
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

        builder.Services.AddOtel(builder.Configuration);

        // PostgreSQL spans (Npgsql's own ActivitySource) - the shared library only instruments
        // ASP.NET Core and outgoing HTTP. A no-op unless a tracing exporter is configured.
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddNpgsql());

        return builder;
    }

    /// <summary>Maps the metrics scraping endpoint when Prometheus is the configured metrics exporter.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    public static WebApplication UseObservability(this WebApplication app)
    {
        app.UseOpenTelemetry();
        return app;
    }
}
