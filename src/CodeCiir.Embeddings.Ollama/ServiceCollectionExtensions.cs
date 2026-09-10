using CodeCiir.Embeddings.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace CodeCiir.Embeddings.Ollama;

public static class ServiceCollectionExtensions
{
    internal const string HttpClientName = "Ollama";

    /// <summary>
    /// Registers the Ollama embedding provider factory. This only makes the "Ollama" provider
    /// available for selection; it is only instantiated if <c>Embeddings:Provider</c> is set to it.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    public static IServiceCollection AddOllamaEmbeddingProvider(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName);
        services.AddSingleton<IEmbeddingProviderFactory, OllamaEmbeddingProviderFactory>();
        return services;
    }
}
