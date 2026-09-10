namespace CodeCiir.Mcp.Tools;

public sealed record CodeQueryGraphEdgeToolResult(
    long? FromId,
    long? ToId,
    string RelationType,
    string TargetSymbol,
    string ResolutionOrigin,
    int Depth);
