namespace CodeCiir.Api.Contracts;

/// <summary>
/// Up to 2 hops of the code relationship graph expanded from a code-queries call's matches -
/// see .specs/05-code-queries-relationship-graph.md. <see cref="Truncated"/> is true when the
/// graph had more nodes than the configured safety cap and was cut off (nearest-depth-first).
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryGraphResponse(
    IReadOnlyList<CodeQueryGraphNodeResponse> Nodes,
    IReadOnlyList<CodeQueryGraphEdgeResponse> Edges,
    bool Truncated);
#pragma warning restore SA1313
