using CodeCiir.Reranking.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace CodeCiir.Reranking.OpenAI;

public static class ServiceCollectionExtensions
{
    internal const string HttpClientName = "OpenAIReranker";

    /// <summary>
    /// Registers the OpenAI-compatible reranking provider factory. This only makes the "OpenAI"
    /// provider available for selection; it is only instantiated if <c>Reranking:Provider</c> is
    /// set to it.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    public static IServiceCollection AddOpenAIRerankerProvider(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName);
        services.AddSingleton<IRerankerProviderFactory, OpenAIRerankerProviderFactory>();
        return services;
    }
}
