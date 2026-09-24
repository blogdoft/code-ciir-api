using CodeCiir.Api.Authentication;
using CodeCiir.Api.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace CodeCiir.Api.Tests.Authentication;

/// <summary>
/// Verifies <see cref="KeycloakAuthenticationExtensions.AddKeycloakAuthentication"/>: its wiring of
/// <see cref="KeycloakOptions.MetadataAddress"/> into the registered <see cref="JwtBearerOptions"/>
/// (without disturbing <see cref="JwtBearerOptions.Authority"/>), its 401/403 events, and - end to
/// end against the real <c>Program</c> pipeline, per .specs/14-keycloak-auth.md - that with Keycloak
/// enabled REST endpoints require a token (fallback policy) while "/mcp" and "/health" stay
/// anonymous, by explicit design (MCP clients here have no way to attach a bearer token). The
/// realm's discovery document is replaced by a fixed symmetric signing key so no real Keycloak is
/// needed - same approach as code-ciir-indexer's Keycloak authentication tests.
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
public sealed class KeycloakAuthenticationExtensionsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string Issuer = "https://keycloak.test/realms/test";

    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes(new string('a', 64)));

    [Fact]
    public void Should_OverrideBearerMetadataAddressButNotAuthority_When_MetadataAddressIsConfigured()
    {
        var options = new KeycloakOptions
        {
            Authority = "https://keycloak.example/realms/blogdoft",
            MetadataAddress = "http://keycloak.internal.svc.cluster.local:8080/realms/blogdoft/.well-known/openid-configuration",
            RequireHttpsMetadata = false,
        };

        var bearer = ResolveJwtBearerOptions(options);

        (bearer.MetadataAddress, bearer.Authority, bearer.RequireHttpsMetadata)
            .ShouldBe((options.MetadataAddress, options.Authority, false));
    }

    [Fact]
    public void Should_DeriveMetadataAddressFromAuthority_When_MetadataAddressIsNotConfigured()
    {
        var options = new KeycloakOptions { Authority = "https://keycloak.example/realms/blogdoft" };

        var bearer = ResolveJwtBearerOptions(options);

        // Not overridden: JwtBearerPostConfigureOptions derives it from Authority itself.
        (bearer.MetadataAddress, bearer.Authority)
            .ShouldBe(("https://keycloak.example/realms/blogdoft/.well-known/openid-configuration", options.Authority));
    }

    [Fact]
    public async Task Should_ReturnUnauthorizedWithChallengeHeaderAndNoBody_When_NoTokenIsSent()
    {
        using var client = CreateEnabledClient();

        using var response = await client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ShouldContain(h => h.Scheme == JwtBearerDefaults.AuthenticationScheme);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnOk_When_AValidTokenIsSent()
    {
        using var client = CreateEnabledClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        using var response = await client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ReturnUnauthorizedWithNoBody_When_TheTokenIsExpired()
    {
        using var client = CreateEnabledClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(lifetime: TimeSpan.FromMinutes(-5)));

        using var response = await client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_NeverChallengeTheMcpEndpoint_When_KeycloakIsEnabledAndNoTokenIsSent()
    {
        // "/mcp" carries no [Authorize]/AllowAnonymous distinction worth asserting on its own -
        // what matters is that the FallbackPolicy that protects every *other* endpoint (proven by
        // Should_ReturnUnauthorizedWithChallengeHeaderAndNoBody_When_NoTokenIsSent above) does not
        // reach it. A 400 for the empty JSON-RPC body proves the request reached the MCP handler
        // instead of being rejected by authentication.
        using var client = CreateEnabledClient();

        using var response = await client.SendAsync(McpRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_NeverChallengeTheMcpEndpoint_When_KeycloakIsDisabled()
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(McpRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_NeverChallengeTheHealthProbe_When_KeycloakIsEnabledAndNoTokenIsSent()
    {
        using var client = CreateEnabledClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_LogWhyTheRequestWasRejectedWithoutSendingItToTheClient_When_NoTokenIsSent()
    {
        var logs = new CapturingLoggerProvider();
        var bearer = ResolveJwtBearerOptions(new KeycloakOptions { Authority = Issuer });
        var httpContext = HttpContextFactory.Create(logs, "GET", "/version");
        var challenge = new JwtBearerChallengeContext(httpContext, Scheme(), bearer, new AuthenticationProperties());

        await bearer.Events.OnChallenge(challenge);

        (httpContext.Response.StatusCode, challenge.Handled).ShouldBe((StatusCodes.Status401Unauthorized, true));
        logs.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task Should_ReplyForbiddenWithNoBodyAndLogTheUsername_When_AnAuthenticatedCallerIsDenied()
    {
        var logs = new CapturingLoggerProvider();
        var bearer = ResolveJwtBearerOptions(new KeycloakOptions { Authority = Issuer });
        var httpContext = HttpContextFactory.Create(logs, "GET", "/version", new Claim("preferred_username", "maria"));
        var forbidden = new ForbiddenContext(httpContext, Scheme(), bearer);

        await bearer.Events.OnForbidden(forbidden);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        var entry = logs.Entries.ShouldHaveSingleItem();
        (entry.Level, entry.Message.Contains("maria", StringComparison.Ordinal)).ShouldBe((LogLevel.Warning, true));
    }

    private static AuthenticationScheme Scheme() =>
        new(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));

    private static JwtBearerOptions ResolveJwtBearerOptions(KeycloakOptions options)
    {
        var services = new ServiceCollection();
        services.AddKeycloakAuthentication(options);
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
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
