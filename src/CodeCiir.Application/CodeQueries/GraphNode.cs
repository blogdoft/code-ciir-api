namespace CodeCiir.Application.CodeQueries;

/// <summary>A code entity (ciir_documents row) reached while expanding the relationship graph.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record GraphNode(
    long Id,
    string Kind,
    string? SymbolContainer,
    string? SymbolName,
    string? SymbolQualifiedName,
    string? SymbolCanonicalName,
    string? SourceFile,
    int Depth);
#pragma warning restore SA1313
