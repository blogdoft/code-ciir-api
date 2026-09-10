namespace CodeCiir.Application.CodeQueries;

/// <summary>
/// A relation (ciir_relations row) directly touching one specific code-queries match, in either
/// direction (who it calls/reads/etc., and who calls/reads/etc. it back). Unlike
/// <see cref="GraphEdge"/> (part of the 2-hop <see cref="CodeGraph"/> expansion, which can drop
/// edges when <see cref="CodeQueryService.MaxGraphNodes"/> truncates the graph), this is always
/// the match's complete set of 1-hop relations, independent of graph size limits.
/// <see cref="ToId"/> is null when the relation's target is outside the indexed universe
/// (external/unresolved - e.g. a framework or dependency symbol).
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record MatchRelation(
    long? FromId,
    long? ToId,
    string RelationType,
    string TargetSymbol,
    string ResolutionOrigin);
#pragma warning restore SA1313
