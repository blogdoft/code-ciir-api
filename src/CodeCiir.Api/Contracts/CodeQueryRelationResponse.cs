using CodeCiir.Application.CodeQueries;

namespace CodeCiir.Api.Contracts;

/// <summary>
/// A relation (ciir_relations row) directly touching one specific match, in either direction.
/// <see cref="ToId"/> is null when the relation's target is outside the indexed universe
/// (external/unresolved - e.g. a framework or dependency symbol); <see cref="TargetSymbol"/>
/// still names it in that case.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryRelationResponse(
    long? FromId,
    long? ToId,
    string RelationType,
    string TargetSymbol,
    string ResolutionOrigin)
{
    public static CodeQueryRelationResponse From(MatchRelation relation) => new(
        relation.FromId, relation.ToId, relation.RelationType, relation.TargetSymbol, relation.ResolutionOrigin);
}
#pragma warning restore SA1313
