using BlogDoFT.Libs.Api.OpenTelemetry.Extensions;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CodeCiir.Api.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>Service name reported to the tracing backend when <c>OTEL_SERVICE_NAME</c> is not set.</summary>
    public const string DefaultServiceName = "code-ciir-api";

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

        // The shared library names the service after the "ApplicationName" configuration key, but
        // that key is reserved by the ASP.NET host and always resolves to the assembly name
        // ("CodeCiir.Api"), so it can't be overridden from configuration. The service name is set
        // here instead, after AddOtel so it wins, keeping traces under the name dashboards and
        // alerts already filter on.
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? DefaultServiceName;

        // PostgreSQL spans (Npgsql's own ActivitySource) - the shared library only instruments
        // ASP.NET Core and outgoing HTTP. A no-op unless a tracing exporter is configured.
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing.AddNpgsql());

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
