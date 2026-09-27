using CodeCiir.Api.Authentication;
using CodeCiir.Api.Extensions;
using CodeCiir.Api.Logging;
using CodeCiir.Application;
using CodeCiir.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// All logs are structured JSON on stdout - the only sink registered, so nothing can fall back to
// human-readable text regardless of environment/configuration.
builder.AddStructuredLogging();

try
{
    builder.AddObservability();

    // --- Keycloak authentication (.specs/14-keycloak-auth.md): opt-in. Null unless
    // "Keycloak:Enabled" is true, in which case no authentication is registered and every endpoint
    // stays open. When enabled, REST controllers require a token by default (fallback policy) -
    // "/mcp" is explicitly exempted further down, regardless of this setting. ---
    var keycloakOptions = KeycloakOptions.FromConfiguration(builder.Configuration);
    if (keycloakOptions is not null)
    {
        builder.Services.AddKeycloakAuthentication(keycloakOptions);
    }

    builder.Services
        .AddApiControllers()
        .AddSwaggerDocumentation(keycloakOptions)
        .AddApplication()
        .AddDatabaseInfrastructure()
        .AddAiProviderServices(builder.Configuration)
        .AddMcpServices();

    var app = builder.Build();

    app.ValidateProviderConfiguration();

    // Ahead of everything else so every request is covered; /health is skipped.
    app.UseStructuredRequestLogging();

    app.UseSwaggerDocumentation(keycloakOptions);
    app.UseObservability();

    // Registered ahead of authentication/authorization: probes must never need a token.
    app.MapHealthProbe();
    app.UseHttpsRedirection();

    if (keycloakOptions is not null)
    {
        app.UseAuthentication();
    }

    app.UseAuthorization();
    app.MapControllers();

    // MCP clients in this deployment have no way to obtain/attach a Keycloak bearer token, so "/mcp"
    // is deliberately exempted from the FallbackPolicy that otherwise protects every endpoint once
    // Keycloak is enabled - a standing exception, not a gap: AllowAnonymous() is a no-op (nothing
    // requires auth) when Keycloak is disabled.
    app.MapMcp("/mcp").AllowAnonymous();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Configuration and database problems are unrecoverable at startup: log why as structured JSON
    // and terminate rather than serve traffic in a broken state. Environment.Exit skips finalizers,
    // so the logger is disposed (which flushes it) before exiting.
    using (var logger = StructuredLoggingExtensions.CreateBootstrapLogger())
    {
        logger.ForContext("SourceContext", "CodeCiir.Api.Program")
            .Fatal(ex, "Application failed to start and will terminate.");
    }

    Environment.Exit(1);
}

/// <summary>Entry point marker so <c>WebApplicationFactory&lt;Program&gt;</c> can bootstrap this API in tests.</summary>
public partial class Program
{
    // WebApplicationFactory<Program> only ever uses this type as a generic marker - which
    // requires a non-static class - and never actually instantiates it, so a protected
    // constructor satisfies Sonar's utility-class check without needing a public one.
    protected Program()
    {
    }
}
