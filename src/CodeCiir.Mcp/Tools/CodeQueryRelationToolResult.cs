namespace CodeCiir.Mcp.Tools;

public sealed record CodeQueryRelationToolResult(
    long? FromId,
    long? ToId,
    string RelationType,
    string TargetSymbol,
    string ResolutionOrigin);
