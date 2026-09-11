namespace CodeCiir.Api.Contracts;

/// <summary>
/// A single code document matched against a natural language query, annotated with its computed
/// similarity score. Field names mirror ciir_documents' own columns rather than code-rag-api's
/// namespace/type_name/member, which have no equivalent in code3rag - see
/// .specs/04-code-queries-baseline.md. Serializes as snake_case.
/// </summary>
/// <param name="Id">Id of the code document (matches <c>ciir_documents.id</c>).</param>
/// <param name="Kind">The document's kind (e.g. <c>method</c>, <c>type</c>, <c>property</c>).</param>
/// <param name="SymbolContainer">Fully qualified name of the container the symbol belongs to.</param>
/// <param name="SymbolName">Short name of the symbol.</param>
/// <param name="SymbolQualifiedName">Dotted qualified name, without parameter types.</param>
/// <param name="SymbolCanonicalName">Full signature, disambiguating overloads.</param>
/// <param name="SourceFile">Path of the source file the symbol was indexed from.</param>
/// <param name="GitUrl">Public Git URL of the project that owns the symbol.</param>
/// <param name="GitRawUrl">
/// Public raw-file URL for the symbol. This is the project's <c>git_raw_url</c> concatenated
/// with the indexed source path; null when the project has no <c>git_raw_url</c>.
/// </param>
/// <param name="EmbeddingText">The text that was actually embedded for this document.</param>
/// <param name="Similarity">Cosine similarity between the question and this document, 0.0-1.0.</param>
/// <param name="RerankScore">
/// Relevance score assigned by the reranking stage, normalized to 0.0-1.0 (higher is more
/// relevant). Null when reranking is disabled (the default) or not configured - see
/// .specs/10-reranking.md.
/// </param>
/// <param name="Relations">
/// This match's complete set of direct (1-hop) relations, in either direction - every relation
/// type, not subject to the 2-hop <c>graph</c>'s node-count truncation. Empty array when the
/// match has no direct relations, or when <c>project_id</c> was omitted from the request (see
/// .specs/12-match-relations.md).
/// </param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryResultResponse(
    long Id,
    string Kind,
    string? SymbolContainer,
    string? SymbolName,
    string? SymbolQualifiedName,
    string? SymbolCanonicalName,
    string? SourceFile,
    Uri? GitUrl,
    Uri? GitRawUrl,
    string? EmbeddingText,
    double Similarity,
    double? RerankScore,
    IReadOnlyList<CodeQueryRelationResponse> Relations);
#pragma warning restore SA1313
