namespace CodeCiir.Reranking.Abstraction;

/// <summary>
/// Raised when a reranking provider fails to score a set of candidates (e.g. the remote service
/// is unreachable, or returns an unexpected response). Treated as an infrastructure failure
/// rather than a domain-modeled <c>Failure</c>, since there is no client-facing recovery for it -
/// it surfaces as a 500 response, the same convention as <c>EmbeddingGenerationException</c>.
/// </summary>
public sealed class RerankingException : Exception
{
    public RerankingException(string message)
        : base(message)
    {
    }

    public RerankingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public RerankingException()
        : base()
    {
    }
}
