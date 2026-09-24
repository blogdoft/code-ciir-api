using CodeCiir.Api.Authentication;
using CodeCiir.Api.Extensions;
using CodeCiir.Application;
using CodeCiir.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

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
