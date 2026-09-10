namespace CodeCiir.Mcp.Tools;

/// <summary>
/// MCP mirror of code-ciir-api's POST .../code-queries response - matches plus up to 2 hops of
/// their code relationship graph. See .specs/05-code-queries-relationship-graph.md.
/// </summary>
public sealed record CodeQueryToolResult(IReadOnlyList<CodeQueryMatchToolResult> Matches, CodeQueryGraphToolResult Graph);
