using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeCiir.Reranking.Abstraction;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="RerankingOptions"/> and registers the resolved <see cref="IReranker"/>.
    /// Call this alongside each provider's own registration extension (e.g.
    /// <c>AddOllamaRerankerProvider</c>) - the provider itself only needs to exist in the
    /// container, this is what decides which one is actually used (or a <see cref="NoOpReranker"/>
    /// when reranking is disabled).
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration root the "Reranking" section is bound from.</param>
    public static IServiceCollection AddRerankingAbstraction(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RerankingOptions>(configuration.GetSection(RerankingOptions.SectionName));
        services.AddSingleton<RerankerResolver>();
        services.AddSingleton(sp => sp.GetRequiredService<RerankerResolver>().Resolve());

        return services;
    }
}
