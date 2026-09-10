namespace CodeCiir.Embeddings.Abstraction;

/// <summary>
/// Raised when an embedding provider fails to produce a vector for a piece of text
/// (e.g. the remote service is unreachable, or returns an unexpected response). This is
/// treated as an infrastructure failure rather than a domain-modeled <c>Failure</c>, since
/// there is no client-facing recovery for it - it surfaces as a 500 response.
/// </summary>
public sealed class EmbeddingGenerationException : Exception
{
    public EmbeddingGenerationException(string message)
        : base(message)
    {
    }

    public EmbeddingGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EmbeddingGenerationException()
        : base()
    {
    }
}
