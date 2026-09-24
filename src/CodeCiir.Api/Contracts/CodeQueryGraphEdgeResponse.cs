using CodeCiir.Application.CodeQueries;

namespace CodeCiir.Api.Contracts;

/// <summary>
/// A relation touching at least one node in the expanded graph. <c>toId</c> is null when the
/// relation's target is outside the indexed universe (external/unresolved - e.g. a framework or
/// dependency symbol); <c>targetSymbol</c> still names it in that case.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryGraphEdgeResponse(
    long? FromId,
    long? ToId,
    string RelationType,
    string TargetSymbol,
    string ResolutionOrigin,
    int Depth)
{
    public static CodeQueryGraphEdgeResponse From(GraphEdge edge) => new(
        edge.FromId, edge.ToId, edge.RelationType, edge.TargetSymbol, edge.ResolutionOrigin, edge.Depth);
}
#pragma warning restore SA1313
