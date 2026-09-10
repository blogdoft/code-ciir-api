namespace CodeCiir.Application.CodeQueries;

/// <summary>
/// Up to <see cref="CodeQueryService.MaxGraphDepth"/> hops of the code relationship graph
/// expanded from a set of root nodes (the code-queries matches). <see cref="Truncated"/> is true
/// when the graph had more than <see cref="CodeQueryService.MaxGraphNodes"/> distinct nodes and
/// was cut off (nearest-depth-first) - see .specs/05-code-queries-relationship-graph.md.
/// </summary>
public sealed record CodeGraph(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges, bool Truncated)
{
    public static readonly CodeGraph Empty = new([], [], false);
}
