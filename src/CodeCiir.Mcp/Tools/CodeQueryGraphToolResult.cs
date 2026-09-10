namespace CodeCiir.Mcp.Tools;

public sealed record CodeQueryGraphToolResult(
    IReadOnlyList<CodeQueryGraphNodeToolResult> Nodes,
    IReadOnlyList<CodeQueryGraphEdgeToolResult> Edges,
    bool Truncated);
