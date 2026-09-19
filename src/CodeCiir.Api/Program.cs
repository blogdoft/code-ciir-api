using CodeCiir.Api.Filters;
using CodeCiir.Application;
using CodeCiir.Embeddings.Abstraction;
using CodeCiir.Embeddings.Ollama;
using CodeCiir.Infrastructure.Database;
using CodeCiir.Reranking.Abstraction;
using CodeCiir.Reranking.Ollama;
using CodeCiir.Reranking.OpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .AddControllers(options =>
        {
            options.Filters.Add<UnhandledExceptionFilter>();

            // Request bodies are always application/json (per the OpenAPI contract) - restrict
            // content negotiation accordingly. There is no equivalent global filter for
            // responses: a global/controller-level [Produces] unconditionally overwrites
            // ObjectResult.ContentTypes - including the "application/problem+json" that
            // UnhandledExceptionFilter sets explicitly on error responses - silently
            // downgrading them to application/json (or 406, depending on the Accept header).
            // Each action instead declares its own 200 content type directly via
            // ProducesResponseType, which only affects that specific status code.
            options.Filters.Add(new ConsumesAttribute("application/json"));
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
        });

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        // Every 400 response - whether raised explicitly by a controller or produced by
        // ASP.NET's own model binding (e.g. a malformed request body) - goes through the same
        // Problem Details shape required by the OpenAPI contract, but this one additionally
        // names which field(s) failed binding and why (see BadRequestValidation) - model binding
        // never reaches a controller action, so domain Failure-based 400s (which already carry a
        // specific message) never go through this path.
        options.InvalidModelStateResponseFactory = context => CodeCiir.Api.Problems.ProblemResults.BadRequestValidation(
            context.ModelState,
            context.HttpContext.Request.Path);

        // Without this, [ApiController] rewrites a bare NotFoundResult into a JSON Problem
        // Details body - the contract requires 404 responses to have no body at all.
        options.SuppressMapClientErrors = true;
    });

    // Request bodies are always plain application/json - the JSON input formatter otherwise
    // also advertises text/json and the application/*+json structured-syntax wildcard as
    // acceptable, which leaks into the generated OpenAPI document's requestBody content types.
    // The output formatter is deliberately left untouched: UnhandledExceptionFilter relies on
    // its application/*+json wildcard support to actually serve application/problem+json error
    // responses. PostConfigure runs after AddControllers has populated the formatter list,
    // regardless of registration order.
    builder.Services.PostConfigure<MvcOptions>(options =>
    {
        foreach (var supportedMediaTypes in options.InputFormatters.OfType<SystemTextJsonInputFormatter>()
            .Select(formatter => formatter.SupportedMediaTypes))
        {
            supportedMediaTypes.Clear();
            supportedMediaTypes.Add("application/json");
        }
    });

    const string ApiDescription = """
        API for querying indexed source code (from code3rag, owned by code-ciir-indexer) using
        natural language, returning not only the most semantically similar code documents but
        also up to two levels of their code relationship graph. See .specs/ for the full
        evolutionary implementation plan - this build only exposes a version endpoint so far.

        All endpoints exclusively accept and return application/json. Client errors and server
        errors are reported using the RFC 7807 "Problem Details for HTTP APIs" format, with the
        exception of 404 responses, which are returned with no response body.
        """;

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new() { Title = "Code CIIR API", Version = CodeCiir.Api.AppVersion.Current, Description = ApiDescription });

        options.TagActionsBy(api =>
        {
            var controllerName = api.ActionDescriptor.RouteValues["controller"];
            return [api.GroupName ?? controllerName ?? "Default"];
        });
        options.DocInclusionPredicate((_, _) => true);

        var xmlDocPath = Path.Combine(AppContext.BaseDirectory, "CodeCiir.Api.xml");
        options.IncludeXmlComments(xmlDocPath);
        options.DocumentFilter<CodeCiir.Api.OpenApi.ControllerTagDescriptionsDocumentFilter>(xmlDocPath);

        // Resolved via the app's IServiceProvider (Swashbuckle instantiates document filters
        // through ActivatorUtilities), so its IConfiguration/IHttpContextAccessor constructor
        // parameters are injected automatically - see PublicServerDocumentFilter for why this
        // exists (blogdoft.home.arpa/code-brain ingress prefix).
        options.DocumentFilter<CodeCiir.Api.OpenApi.PublicServerDocumentFilter>();
    });

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddApplication();
    builder.Services.AddDatabaseInfrastructure();

    // Export request, outgoing HTTP (Ollama/OpenAI) and PostgreSQL spans through the cluster's
    // OTLP collector. Exporter options, including endpoint and protocol, come from the standard
    // OTEL_* environment variables in code-ciir-config; the collector enriches and forwards them
    // to Tempo (see the observability manifests).
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            builder.Configuration["OTEL_SERVICE_NAME"] ?? "code-ciir-api"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddNpgsql()
            .AddOtlpExporter());

    builder.Services.AddEmbeddingAbstraction(builder.Configuration);
    builder.Services.AddOllamaEmbeddingProvider();

    builder.Services.AddRerankingAbstraction(builder.Configuration);
    builder.Services.AddOllamaRerankerProvider();
    builder.Services.AddOpenAIRerankerProvider();

    // Exposes the same Projects/Code Query functionality as MCP tools, for LLM clients doing
    // code research, alongside the REST API. Stateless by default: no session affinity needed
    // since these tools never need to message the client back (no sampling/elicitation).
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .AddCodeCiirTools();

    var app = builder.Build();

    // Fail fast: catches a missing/unknown Embeddings:Provider (or Model/Dimensions) at startup
    // instead of surfacing as a raw 500 on the first /code-queries request.
    app.Services.GetRequiredService<EmbeddingGeneratorResolver>().ValidateProviderConfigured();

    // Same fail-fast rationale as above, with one difference: an empty/"None" Reranking:Provider
    // is a valid, supported "disabled" configuration and resolves to a NoOpReranker without
    // throwing - only an unknown, non-empty provider name (a config typo) crashes startup here.
    app.Services.GetRequiredService<IReranker>();

    // Always mapped (not gated to Development) so Swagger is reachable in this cluster too - both
    // routes live under "api/code-queries" since that's the only prefix the blogdoft.home.arpa/
    // code-brain ingress forwards to this service (see .eng/k8s/ingress.yaml). The swagger.json
    // URL passed to SwaggerEndpoint is relative ("v1/swagger.json"), so the browser resolves it
    // against whatever prefix it is actually browsing under (locally or through the ingress)
    // without the app needing to know about that prefix itself.
    app.UseSwagger(options => options.RouteTemplate = "api/code-queries/swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("v1/swagger.json", "Code CIIR API v1");
        options.RoutePrefix = "api/code-queries/swagger";
    });

    // Keep probe traffic out of the request-logging middleware. This is intentionally a
    // terminal branch rather than an MVC endpoint so Kubernetes' frequent checks never emit
    // request logs, irrespective of the configured Serilog level.
    app.Map("/health", healthApp => healthApp.Run(context =>
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        return Task.CompletedTask;
    }));

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    app.MapMcp("/mcp");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
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
