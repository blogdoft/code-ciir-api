namespace CodeCiir.Mcp.Tools;

public sealed record CodeQueryMatchToolResult(
    long Id,
    string Kind,
    string? SymbolContainer,
    string? SymbolName,
    string? SymbolQualifiedName,
    string? SymbolCanonicalName,
    string? SourceFile,
    string? EmbeddingText,
    double Similarity,
    double? RerankScore,
    IReadOnlyList<CodeQueryRelationToolResult> Relations);
