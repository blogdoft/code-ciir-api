using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace CodeCiir.Api.Authentication;

/// <summary>Registers Keycloak-issued JWT bearer authentication (.specs/14-keycloak-auth.md).</summary>
public static class KeycloakAuthenticationExtensions
{
    /// <summary>
    /// Requires a valid Keycloak access token on every endpoint by default: registers the JWT bearer
    /// scheme against the realm and sets the authorization fallback policy to "authenticated user",
    /// so a route is protected unless it explicitly opts out with <c>AllowAnonymous</c> - rather than
    /// open unless someone remembers an attribute. The caller (Program.cs) opts the MCP endpoint
    /// ("/mcp") out this way: MCP clients (that protocol has no bearer-token exchange of its own in
    /// this deployment) must keep working unauthenticated even when Keycloak is enabled.
    /// </summary>
    /// <param name="services">The service collection to add authentication to.</param>
    /// <param name="options">The validated Keycloak options (see <see cref="KeycloakOptions.FromConfiguration"/>).</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, KeycloakOptions options)
    {
        services.AddSingleton(options);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.Authority = options.Authority;

                // MetadataAddress, when set, overrides *where the JWKS/discovery document come from*
                // only - Authority stays the public URL used for issuer validation (Keycloak's
                // discovery document reports its own public issuer no matter which address served
                // it) and for the Swagger "Authorize" button's login endpoint.
                if (options.MetadataAddress.Length > 0)
                {
                    bearer.MetadataAddress = options.MetadataAddress;
                }

                bearer.RequireHttpsMetadata = options.RequireHttpsMetadata;
                bearer.MapInboundClaims = false;

                // See KeycloakOptions.SkipCertificateValidation: only needed when the JWKS host's CA
                // isn't trusted by this container and that trust can't be fixed directly (yet).
                if (options.SkipCertificateValidation)
                {
#pragma warning disable S4830 // deliberate, opt-in via SkipCertificateValidation - see KeycloakOptions
                    bearer.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                    };
#pragma warning restore S4830
                }

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = options.Audience.Length > 0,
                    ValidAudience = options.Audience.Length > 0 ? options.Audience : null,
                };
                bearer.Events = new JwtBearerEvents { OnChallenge = RejectUnauthenticated, OnForbidden = RejectForbidden };
            });

        services.AddAuthorization(authorization =>
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    // 401 carries no response body - the WWW-Authenticate header alone tells the client to send a
    // Bearer token. Why a token was rejected (signature, issuer, expiry, ...) is deliberately never
    // revealed to the client; it is only recorded in the application log.
    private static Task RejectUnauthenticated(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;

        GetLogger(context.HttpContext).LogInformation(
            "Request {Method} {Path} rejected with 401: missing or invalid access token ({AuthenticationError})",
            context.Request.Method,
            context.Request.Path,
            context.AuthenticateFailure?.GetType().Name ?? "no token");

        return Task.CompletedTask;
    }

    // 403 likewise carries no body; the authenticated caller is logged so a denied access can be
    // traced back to whoever attempted it.
    private static Task RejectForbidden(ForbiddenContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        GetLogger(context.HttpContext).LogWarning(
            "Request {Method} {Path} forbidden for {Username}",
            context.Request.Method,
            context.Request.Path,
            context.HttpContext.User.GetUsername());

        return Task.CompletedTask;
    }

    private static ILogger GetLogger(HttpContext httpContext) => httpContext.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger(typeof(KeycloakAuthenticationExtensions).FullName!);
}
