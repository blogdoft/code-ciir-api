namespace CodeCiir.Reranking.Abstraction;

/// <summary>A code document offered up for reranking, identified by its id, described by its text.</summary>
/// <param name="Id">Id of the code document (matches <c>ciir_documents.id</c>).</param>
/// <param name="Text">The text the reranker should judge for relevance (the document's <c>embedding_text</c>).</param>
public sealed record RerankCandidate(long Id, string Text);
