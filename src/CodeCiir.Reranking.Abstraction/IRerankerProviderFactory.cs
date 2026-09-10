namespace CodeCiir.Reranking.Abstraction;

/// <summary>
/// Creates an <see cref="IReranker"/> for a specific provider. Each provider module (Ollama,
/// OpenAI-compatible, ...) registers one factory into DI; the <see cref="RerankerResolver"/>
/// picks whichever matches the configured <see cref="RerankingOptions.Provider"/> value. New
/// providers plug in without any change to the resolver.
/// </summary>
public interface IRerankerProviderFactory
{
    /// <summary>Provider name this factory handles, matched case-insensitively against configuration.</summary>
    string ProviderName { get; }

    /// <summary>Builds a reranker for this provider using the supplied connection options.</summary>
    /// <param name="options">Connection settings for the reranker, bound from the "Reranking" section.</param>
    IReranker Create(RerankingOptions options);
}
