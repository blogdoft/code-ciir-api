namespace CodeCiir.Embeddings.Abstraction;

/// <summary>
/// Creates an <see cref="IEmbeddingGenerator"/> for a specific provider, scoped to one
/// model/dimensions pair. Each provider module (Ollama, ...) registers one factory into DI; the
/// <see cref="EmbeddingGeneratorResolver"/> picks whichever matches the configured
/// <see cref="EmbeddingOptions.Provider"/> value and caches the resulting generator per
/// model/dimensions pair. New providers plug in without any change to the resolver.
/// </summary>
public interface IEmbeddingProviderFactory
{
    /// <summary>Provider name this factory handles, matched case-insensitively against configuration.</summary>
    string ProviderName { get; }

    /// <summary>Builds a generator for this provider using the supplied connection options and model.</summary>
    /// <param name="options">Connection settings for the generator, bound from the "Embeddings" section.</param>
    /// <param name="model">Name of the embedding model to request from the provider.</param>
    /// <param name="dimensions">Expected dimensionality of vectors produced by <paramref name="model"/>.</param>
    IEmbeddingGenerator Create(EmbeddingOptions options, string model, int dimensions);
}
