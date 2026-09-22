using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CodeCiir.Api.Tests.OpenApi;

/// <summary>
/// Verifies KeycloakSecurityDocumentFilter's effect on the published OpenAPI document through the
/// real Swagger endpoint (there is no InternalsVisibleTo wiring from CodeCiir.Api into this test
/// project, matching how the other internal document filters here - e.g. PublicServerDocumentFilter
/// - are exercised, so this goes through HTTP rather than instantiating the filter directly).
///
/// "Keycloak:*" is turned on via environment variables rather than <c>WithWebHostBuilder</c>'s
/// <c>ConfigureAppConfiguration</c> - see the remarks on KeycloakAuthenticationTests for why.
/// </summary>
public sealed class KeycloakSecurityDocumentFilterTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string SwaggerJsonPath = "/api/code-queries/swagger/v1/swagger.json";

    [Fact]
    public async Task SwaggerJson_KeycloakDisabled_HasNoSecuritySchemes()
    {
        using var client = factory.CreateClient();

        using var document = await FetchDocumentAsync(client);

        document.RootElement.GetProperty("components").TryGetProperty("securitySchemes", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task SwaggerJson_KeycloakEnabledWithoutClientId_HasOnlyTheBearerScheme()
    {
        using var client = CreateEnabledClient(clientId: null);

        using var document = await FetchDocumentAsync(client);

        var securitySchemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
        securitySchemes.TryGetProperty("Bearer", out var bearer).ShouldBeTrue();
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
        securitySchemes.TryGetProperty("OAuth2", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task SwaggerJson_KeycloakEnabledWithClientId_AlsoHasTheOAuth2Scheme()
    {
        using var client = CreateEnabledClient(clientId: "swagger");

        using var document = await FetchDocumentAsync(client);

        var securitySchemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
        var oauth2 = securitySchemes.GetProperty("OAuth2");
        oauth2.GetProperty("type").GetString().ShouldBe("oauth2");
        oauth2.GetProperty("flows").GetProperty("authorizationCode").GetProperty("scopes")
            .TryGetProperty("openid", out _).ShouldBeTrue();
    }

    private static async Task<JsonDocument> FetchDocumentAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri(SwaggerJsonPath, UriKind.Relative));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private HttpClient CreateEnabledClient(string? clientId)
    {
        const string issuer = "https://keycloak.test/realms/test";

        Environment.SetEnvironmentVariable("Keycloak__Enabled", "true");
        Environment.SetEnvironmentVariable("Keycloak__Authority", issuer);
        Environment.SetEnvironmentVariable("Keycloak__ClientId", clientId);
        try
        {
            // Swagger generation never validates a token, but AddKeycloakAuthentication still
            // wires up JwtBearer's discovery-document fetch unless Configuration is preset here -
            // same reasoning as KeycloakAuthenticationTests.
            return factory
                .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.Configure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    bearer => bearer.Configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = issuer,
                        SigningKeys = { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('a', 64))) },
                    })))
                .CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Keycloak__Enabled", null);
            Environment.SetEnvironmentVariable("Keycloak__Authority", null);
            Environment.SetEnvironmentVariable("Keycloak__ClientId", null);
        }
    }
}
