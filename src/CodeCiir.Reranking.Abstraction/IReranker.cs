namespace CodeCiir.Reranking.Abstraction;

/// <summary>
/// Reorders a set of candidate code documents by their relevance to a natural language query.
/// Implementations wrap a specific provider (Ollama, OpenAI-compatible, ...); callers depend
/// only on this abstraction.
/// </summary>
public interface IReranker
{
    /// <summary>Name of the provider backing this instance (e.g. "Ollama", "OpenAI", "None").</summary>
    string Provider { get; }

    /// <summary>
    /// How many extra candidates, beyond the caller's requested result count, the vector search
    /// should fetch before reranking - so relevant candidates ranked outside the raw
    /// cosine-similarity top-N still get a chance to be promoted by the reranker. <c>0</c> when
    /// reranking is disabled (no widening needed).
    /// </summary>
    int CandidatePoolSize { get; }

    /// <summary>
    /// Scores every candidate's relevance to <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The natural language query the candidates are being ranked against.</param>
    /// <param name="candidates">The candidates to score, in their pre-reranking (vector-similarity) order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// One <see cref="RerankedCandidate"/> per input candidate, not necessarily reordered by the
    /// implementation itself - callers are responsible for sorting by <see cref="RerankedCandidate.Score"/>.
    /// </returns>
    /// <exception cref="RerankingException">
    /// The provider failed to score the candidates (unreachable service, malformed response, etc.).
    /// </exception>
    Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query, IReadOnlyList<RerankCandidate> candidates, CancellationToken cancellationToken = default);
}
