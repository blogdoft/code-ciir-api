using CodeCiir.Reranking.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace CodeCiir.Reranking.Ollama;

public static class ServiceCollectionExtensions
{
    internal const string HttpClientName = "OllamaReranker";

    /// <summary>
    /// Registers the Ollama reranking provider factory. This only makes the "Ollama" provider
    /// available for selection; it is only instantiated if <c>Reranking:Provider</c> is set to it.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    public static IServiceCollection AddOllamaRerankerProvider(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName);
        services.AddSingleton<IRerankerProviderFactory, OllamaRerankerProviderFactory>();
        return services;
    }
}
