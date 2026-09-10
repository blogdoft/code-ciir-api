namespace CodeCiir.Reranking.Abstraction;

/// <summary>
/// Passthrough reranker used when reranking is disabled (<see cref="RerankingOptions.Provider"/>
/// empty or "None") - returns every candidate unscored, in its original (pre-reranking) order, so
/// callers that always invoke <see cref="IReranker"/> unconditionally get identical behavior to
/// not having a reranker at all.
/// </summary>
internal sealed class NoOpReranker : IReranker
{
    public string Provider => "None";

    public int CandidatePoolSize => 0;

    public Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query, IReadOnlyList<RerankCandidate> candidates, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RerankedCandidate>>(
            candidates.Select(c => new RerankedCandidate(c.Id, null)).ToList());
}
