namespace CodeCiir.Api.Contracts;

/// <summary>
/// A relation touching at least one node in the expanded graph. <c>to_id</c> is null when the
/// relation's target is outside the indexed universe (external/unresolved - e.g. a framework or
/// dependency symbol); <c>target_symbol</c> still names it in that case.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryGraphEdgeResponse(
    long? FromId,
    long? ToId,
    string RelationType,
    string TargetSymbol,
    string ResolutionOrigin,
    int Depth);
#pragma warning restore SA1313
