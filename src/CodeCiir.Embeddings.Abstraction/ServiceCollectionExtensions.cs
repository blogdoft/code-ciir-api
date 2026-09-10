using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeCiir.Embeddings.Abstraction;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="EmbeddingOptions"/> and registers the <see cref="EmbeddingGeneratorResolver"/>.
    /// Call this alongside each provider's own registration extension (e.g.
    /// <c>AddOllamaEmbeddingProvider</c>) - the provider itself only needs to exist in the
    /// container, this is what decides which one is actually used.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration root the "Embeddings" section is bound from.</param>
    public static IServiceCollection AddEmbeddingAbstraction(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));
        services.AddSingleton<EmbeddingGeneratorResolver>();

        return services;
    }
}
