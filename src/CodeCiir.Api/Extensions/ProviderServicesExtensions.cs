using CodeCiir.Embeddings.Abstraction;
using CodeCiir.Embeddings.Ollama;
using CodeCiir.Mcp;
using CodeCiir.Reranking.Abstraction;
using CodeCiir.Reranking.Ollama;
using CodeCiir.Reranking.OpenAI;

namespace CodeCiir.Api.Extensions;

public static class ProviderServicesExtensions
{
    /// <summary>Registers the embedding and reranking abstractions and every supported provider.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration (Embeddings/Reranking sections).</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddAiProviderServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEmbeddingAbstraction(configuration);
        services.AddOllamaEmbeddingProvider();

        services.AddRerankingAbstraction(configuration);
        services.AddOllamaRerankerProvider();
        services.AddOpenAIRerankerProvider();

        return services;
    }

    /// <summary>
    /// Exposes the same Projects/Code Query functionality as MCP tools, for LLM clients doing code
    /// research, alongside the REST API. Stateless by default: no session affinity needed since
    /// these tools never need to message the client back (no sampling/elicitation).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddMcpServices(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithHttpTransport()
            .AddCodeCiirTools();

        return services;
    }

    /// <summary>
    /// Fail fast on misconfigured providers: catches a missing/unknown <c>Embeddings:Provider</c>
    /// (or Model/Dimensions) at startup instead of surfacing as a raw 500 on the first request. An
    /// empty/"None" <c>Reranking:Provider</c> is a valid "disabled" configuration that resolves to
    /// a NoOpReranker without throwing - only an unknown, non-empty provider name (a config typo)
    /// crashes startup.
    /// </summary>
    /// <param name="app">The built web application.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    public static WebApplication ValidateProviderConfiguration(this WebApplication app)
    {
        app.Services.GetRequiredService<EmbeddingGeneratorResolver>().ValidateProviderConfigured();
        app.Services.GetRequiredService<IReranker>();
        return app;
    }
}
