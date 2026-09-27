namespace CodeCiir.Application.CodeQueries;

public interface IRelationshipGraphRepository
{
    /// <summary>
    /// Expands the code relationship graph up to <paramref name="maxDepth"/> hops from
    /// <paramref name="rootIds"/>, considering every relation type and both directions (who a
    /// node references, and who references it). Caps the total distinct nodes returned at
    /// <paramref name="maxNodes"/>, favoring the smallest-depth nodes first.
    /// </summary>
    /// <param name="projectId">The project the root documents belong to.</param>
    /// <param name="rootIds">The document ids to expand the graph from.</param>
    /// <param name="maxDepth">Maximum number of hops to traverse from any root id.</param>
    /// <param name="maxNodes">Maximum number of distinct nodes to return.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
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
    /// <param name="projectId">The project the documents belong to.</param>
    /// <param name="documentIds">The document ids to look up direct relations for.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
    Task<IReadOnlyDictionary<long, IReadOnlyList<MatchRelation>>> GetDirectRelationsAsync(
        long projectId,
        IReadOnlyList<long> documentIds,
        CancellationToken cancellationToken = default);
}
