using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace CodeCiir.Api.Tests.Authentication;

/// <summary>
/// End-to-end verification of .specs/14-keycloak-auth.md against the real <c>Program</c> pipeline:
/// with Keycloak enabled, REST endpoints require a token (fallback policy) while "/mcp" stays
/// anonymous, by explicit design (MCP clients here have no way to attach a bearer token). The
/// realm's discovery document is replaced by a fixed symmetric signing key so no real Keycloak is
/// needed - same approach as code-ciir-indexer's KeycloakAuthenticationTests.
///
/// "Keycloak:*" is turned on via process environment variables, not <c>WithWebHostBuilder</c>'s
/// <c>ConfigureAppConfiguration</c>: <c>Program.cs</c> reads <c>KeycloakOptions.FromConfiguration</c>
/// eagerly, before <c>builder.Build()</c>, and <c>WebApplicationFactory</c> only splices test
/// configuration into the builder at that same <c>Build()</c> call - too late for this particular
/// read. An environment variable, set before <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()"/>
/// triggers <c>Program.cs</c>, is visible from the very first line
/// (<c>WebApplication.CreateBuilder(args)</c> loads it as a configuration source immediately) and is
/// restored right after, which is safe only because xunit.runner.json disables collection
/// parallelism for this assembly (no other test can observe the mutation in between).
/// </summary>
public sealed class KeycloakAuthenticationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string Issuer = "https://keycloak.test/realms/test";

    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes(new string('a', 64)));

    [Fact]
    public async Task GetVersion_KeycloakEnabledNoToken_ReturnsUnauthorizedProblemDetails()
    {
        using var client = CreateEnabledClient();

        using var response = await client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ShouldContain(h => h.Scheme == JwtBearerDefaults.AuthenticationScheme);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetVersion_KeycloakEnabledValidToken_ReturnsOk()
    {
        using var client = CreateEnabledClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        using var response = await client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetVersion_KeycloakEnabledExpiredToken_ReturnsUnauthorized()
    {
        using var client = CreateEnabledClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(lifetime: TimeSpan.FromMinutes(-5)));

        using var response = await client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostMcp_KeycloakEnabledNoToken_IsNeverChallenged()
    {
        // "/mcp" carries no [Authorize]/AllowAnonymous distinction worth asserting on its own -
        // what matters is that the FallbackPolicy that protects every *other* endpoint (proven by
        // GetVersion_KeycloakEnabledNoToken_ReturnsUnauthorizedProblemDetails above) does not reach
        // it. A 400 for the empty JSON-RPC body proves the request reached the MCP handler instead
        // of being rejected by authentication.
        using var client = CreateEnabledClient();

        using var response = await client.SendAsync(McpRequest());

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostMcp_KeycloakDisabled_IsAlsoNeverChallenged()
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(McpRequest());

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static string CreateToken(TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            NotBefore = now - TimeSpan.FromHours(3),
            Expires = now + (lifetime ?? TimeSpan.FromMinutes(5)),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        });
    }

    // The MCP Streamable HTTP transport requires this Accept header on every request, independent
    // of authentication - without it the endpoint answers 406 (not the 400 these tests key on for
    // "reached the MCP handler, unauthenticated").
    private static HttpRequestMessage McpRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/mcp", UriKind.Relative))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private HttpClient CreateEnabledClient()
    {
        Environment.SetEnvironmentVariable("Keycloak__Enabled", "true");
        Environment.SetEnvironmentVariable("Keycloak__Authority", Issuer);
        try
        {
            // Configure (not PostConfigure): the handler's own post-configuration only builds a
            // discovery-based configuration manager when Configuration isn't already set.
            return factory
                .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.Configure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    bearer => bearer.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer, SigningKeys = { SigningKey } })))
                .CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Keycloak__Enabled", null);
            Environment.SetEnvironmentVariable("Keycloak__Authority", null);
        }
    }
}
