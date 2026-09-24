using CodeCiir.Application.CodeQueries;

namespace CodeCiir.Api.Contracts;

/// <summary>A code entity reached while expanding the relationship graph from a match.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryGraphNodeResponse(
    long Id,
    string Kind,
    string? SymbolContainer,
    string? SymbolName,
    string? SymbolQualifiedName,
    string? SymbolCanonicalName,
    string? SourceFile,
    int Depth)
{
    public static CodeQueryGraphNodeResponse From(GraphNode node) => new(
        node.Id,
        node.Kind,
        node.SymbolContainer,
        node.SymbolName,
        node.SymbolQualifiedName,
        node.SymbolCanonicalName,
        node.SourceFile,
        node.Depth);
}
#pragma warning restore SA1313
