namespace CodeCiir.Application.CodeQueries;

/// <summary>
/// A single code entity (ciir_documents row) matched against a natural language query. Field
/// names mirror ciir_documents' own columns (symbol_container/symbol_name/...) rather than
/// code-rag-api's namespace/type_name/member, which have no equivalent in code3rag - see
/// .specs/04-code-queries-baseline.md.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryResult(
    long Id,
    string Kind,
    string? SymbolContainer,
    string? SymbolName,
    string? SymbolQualifiedName,
    string? SymbolCanonicalName,
    string? SourceFile,
    string? EmbeddingText,
    double Similarity,
    double? RerankScore = null,
    IReadOnlyList<MatchRelation>? Relations = null);
#pragma warning restore SA1313
