namespace CodeCiir.Application.CodeQueries;

/// <summary>
/// A relation (ciir_relations row) touching at least one node in the expanded graph.
/// <see cref="ToId"/> is null when the relation's target is outside the indexed universe
/// (external/unresolved, e.g. a framework or dependency symbol) - see
/// .specs/05-code-queries-relationship-graph.md.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record GraphEdge(
    long? FromId,
    long? ToId,
    string RelationType,
    string TargetSymbol,
    string ResolutionOrigin,
    int Depth);
#pragma warning restore SA1313
