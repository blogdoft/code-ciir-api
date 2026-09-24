using CodeCiir.Api.Authentication;
using CodeCiir.Api.OpenApi;

namespace CodeCiir.Api.Extensions;

public static class SwaggerExtensions
{
    private const string ApiDescription = """
        API for querying indexed source code (from code3rag, owned by code-ciir-indexer) using
        natural language, returning not only the most semantically similar code documents but
        also up to two levels of their code relationship graph. See .specs/ for the full
        evolutionary implementation plan.

        All endpoints exclusively accept and return application/json (the feedback export is the
        one exception and returns text/csv), with camelCase property names. Client errors (400)
        are reported using the RFC 7807 "Problem Details for HTTP APIs" format. 401, 403, 404 and
        5xx responses are returned with no response body.
        """;

    /// <summary>Registers Swashbuckle: document metadata, XML docs, tags, servers and (when Keycloak is on) security schemes.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="keycloakOptions">Keycloak settings, or <see langword="null"/> when authentication is disabled.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services, KeycloakOptions? keycloakOptions)
    {
        services.AddHttpContextAccessor();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "Code CIIR API", Version = AppVersion.Current, Description = ApiDescription });

            options.TagActionsBy(api =>
            {
                var controllerName = api.ActionDescriptor.RouteValues["controller"];
                return [api.GroupName ?? controllerName ?? "default"];
            });
            options.DocInclusionPredicate((_, _) => true);

            var xmlDocPath = Path.Combine(AppContext.BaseDirectory, "CodeCiir.Api.xml");
            options.IncludeXmlComments(xmlDocPath);
            options.DocumentFilter<ControllerTagDescriptionsDocumentFilter>(xmlDocPath);
            options.SchemaFilter<RequestExamplesSchemaFilter>();

            // Resolved via the app's IServiceProvider (Swashbuckle instantiates document filters
            // through ActivatorUtilities), so its IConfiguration/IHttpContextAccessor constructor
            // parameters are injected automatically - see PublicServerDocumentFilter for why this
            // exists (blogdoft.home.arpa/code-brain ingress prefix).
            options.DocumentFilter<PublicServerDocumentFilter>();

            // Only when Keycloak is on: documents how to authenticate against the REST controllers
            // this Swagger document actually describes ("/mcp" is a separate, always-anonymous
            // endpoint and isn't part of this document).
            if (keycloakOptions is not null)
            {
                options.DocumentFilter<KeycloakSecurityDocumentFilter>();
            }
        });

        return services;
    }

    /// <summary>
    /// Always mapped (not gated to Development) so Swagger is reachable in the cluster too - both
    /// routes live under "api/code-queries" since that's the only prefix the
    /// blogdoft.home.arpa/code-brain ingress forwards to this service (see .eng/k8s/ingress.yaml).
    /// The swagger.json URL passed to SwaggerEndpoint is relative ("v1/swagger.json"), so the
    /// browser resolves it against whatever prefix it is actually browsing under (locally or
    /// through the ingress) without the app needing to know about that prefix itself.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="keycloakOptions">Keycloak settings, or <see langword="null"/> when authentication is disabled.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    public static WebApplication UseSwaggerDocumentation(this WebApplication app, KeycloakOptions? keycloakOptions)
    {
        app.UseSwagger(options => options.RouteTemplate = "api/code-queries/swagger/{documentName}/swagger.json");
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("v1/swagger.json", "Code CIIR API v1");
            options.RoutePrefix = "api/code-queries/swagger";

            // "Authorize" redirects to the Keycloak login (authorization code + PKCE, public
            // client) when a client id is configured (the API and Swagger UI share one Keycloak
            // client); the redirect_uri, oauth2-redirect.html under this same prefix, is computed
            // by the browser from the current URL.
            if (keycloakOptions is { ClientId.Length: > 0 })
            {
                options.OAuthClientId(keycloakOptions.ClientId);
                options.OAuthUsePkce();
                options.OAuthScopes(KeycloakSecurityDocumentFilter.OpenIdScope);
            }
        });

        return app;
    }
}
