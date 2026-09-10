namespace CodeCiir.Mcp.Tools;

public sealed record CodeQueryGraphNodeToolResult(
    long Id,
    string Kind,
    string? SymbolContainer,
    string? SymbolName,
    string? SymbolQualifiedName,
    string? SymbolCanonicalName,
    string? SourceFile,
    int Depth);
