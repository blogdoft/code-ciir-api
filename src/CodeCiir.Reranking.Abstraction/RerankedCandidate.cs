namespace CodeCiir.Reranking.Abstraction;

/// <summary>A candidate's relevance score after reranking.</summary>
/// <param name="Id">Id of the code document this score belongs to, matching the corresponding <see cref="RerankCandidate.Id"/>.</param>
/// <param name="Score">
/// Relevance score normalized to 0.0-1.0 (higher is more relevant), or <see langword="null"/>
/// when the candidate was not scored (e.g. reranking is disabled).
/// </param>
public sealed record RerankedCandidate(long Id, double? Score);
