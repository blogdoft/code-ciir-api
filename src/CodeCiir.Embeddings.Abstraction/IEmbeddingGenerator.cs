namespace CodeCiir.Embeddings.Abstraction;

/// <summary>
/// Converts natural language text into a vector embedding. Implementations wrap a specific
/// provider (Ollama, OpenAI, ...); callers depend only on this abstraction.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>Name of the provider backing this generator (e.g. "Ollama").</summary>
    string Provider { get; }

    /// <summary>Name of the model used to generate embeddings for this instance.</summary>
    string Model { get; }

    /// <summary>Number of components in every vector this generator produces.</summary>
    int Dimensions { get; }

    /// <summary>
    /// Generates an embedding for the given text.
    /// </summary>
    /// <param name="text">Text to embed.</param>
    /// <param name="cancellationToken">Token used to cancel the generation.</param>
    /// <exception cref="EmbeddingGenerationException">
    /// The provider failed to generate an embedding (unreachable service, malformed response, etc.).
    /// </exception>
    Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default);
}
