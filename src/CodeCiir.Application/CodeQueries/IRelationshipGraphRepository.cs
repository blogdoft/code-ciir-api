namespace CodeCiir.Application.CodeQueries;

public interface IRelationshipGraphRepository
{
    /// <summary>
    /// Expands the code relationship graph up to <paramref name="maxDepth"/> hops from
    /// <paramref name="rootIds"/>, considering every relation type and both directions (who a
    /// node references, and who references it). Caps the total distinct nodes returned at
    /// <paramref name="maxNodes"/>, favoring the smallest-depth nodes first.
    /// </summary>
    Task<CodeGraph> GetGraphAsync(
        long projectId,
        IReadOnlyList<long> rootIds,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns, for each id in <paramref name="documentIds"/>, its complete set of direct (1-hop)
    /// relations in either direction - not subject to any graph-size truncation. An id with no
    /// direct relations is simply absent from the result (never a key with an empty list).
    /// </summary>
    Task<IReadOnlyDictionary<long, IReadOnlyList<MatchRelation>>> GetDirectRelationsAsync(
        long projectId,
        IReadOnlyList<long> documentIds,
        CancellationToken cancellationToken = default);
}
