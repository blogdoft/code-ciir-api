using CodeCiir.Api.Authentication;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodeCiir.Api.OpenApi;

/// <summary>
/// Declares how a caller authenticates on the OpenAPI document, so Swagger UI offers an "Authorize"
/// button: always a <c>Bearer</c> (JWT) scheme where an access token is pasted, plus - when
/// <see cref="KeycloakOptions.ClientId"/> is configured - an <c>OAuth2</c> authorization-code
/// scheme that sends the user to the Keycloak login page instead. Either satisfies the global
/// security requirement applied to REST controllers. Only registered when Keycloak authentication
/// is enabled; without it the document carries no security information. Mirrors
/// code-ciir-indexer's KeycloakSecurityDocumentTransformer (the Microsoft.AspNetCore.OpenApi
/// equivalent of this Swashbuckle IDocumentFilter). Deliberately says nothing about "/mcp": that
/// endpoint never requires a token (see KeycloakAuthenticationExtensions), and it is not part of
/// this Swagger document anyway (it isn't an MVC controller).
/// </summary>
/// <param name="options">The validated Keycloak options, from which the realm's login/token URLs are derived.</param>
internal sealed class KeycloakSecurityDocumentFilter(KeycloakOptions options) : IDocumentFilter
{
    /// <summary>The key the paste-a-token scheme is registered under in the document's security schemes.</summary>
    internal const string BearerSchemeName = "Bearer";

    /// <summary>The key the Keycloak login scheme is registered under in the document's security schemes.</summary>
    internal const string OAuth2SchemeName = "OAuth2";

    /// <summary>The only scope requested: enough for Keycloak to issue an ID/access token pair for the logged-in user.</summary>
    internal const string OpenIdScope = "openid";

    /// <summary>Adds the security schemes, and the global requirement satisfied by any of them, to <paramref name="swaggerDoc"/>.</summary>
    /// <param name="swaggerDoc">The OpenAPI document being built.</param>
    /// <param name="context">Unused - the schemes are derived from the Keycloak options, not the API description.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Components ??= new OpenApiComponents();
        swaggerDoc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        swaggerDoc.Security ??= [];

        swaggerDoc.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Keycloak access token. Paste the token only, without the 'Bearer ' prefix.",
        };
        swaggerDoc.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSchemeName, swaggerDoc)] = [],
        });

        // Every REST operation in this document sits behind the authenticated-user fallback policy,
        // so any of them can answer 401 (missing/invalid token) or 403 (authenticated but not
        // allowed). Both carry no response body.
        foreach (var operation in swaggerDoc.Paths.Values.SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>()))
        {
            operation.Responses ??= [];
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing, expired or invalid access token. No response body." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Authenticated, but not allowed to perform this operation. No response body." });
        }

        if (options.ClientId.Length > 0)
        {
            var openIdConnect = $"{options.Authority.TrimEnd('/')}/protocol/openid-connect";
            swaggerDoc.Components.SecuritySchemes[OAuth2SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Description = "Log in through Keycloak (authorization code flow with PKCE).",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{openIdConnect}/auth"),
                        TokenUrl = new Uri($"{openIdConnect}/token"),
                        Scopes = new Dictionary<string, string> { [OpenIdScope] = "OpenID Connect login" },
                    },
                },
            };
            swaggerDoc.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(OAuth2SchemeName, swaggerDoc)] = [OpenIdScope],
            });
        }
    }
}
