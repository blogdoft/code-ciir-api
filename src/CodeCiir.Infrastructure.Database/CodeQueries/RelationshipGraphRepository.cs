using CodeCiir.Application.CodeQueries;
using Dapper;
using Npgsql;

namespace CodeCiir.Infrastructure.Database.CodeQueries;

/// <summary>
/// Traverses public.ciir_relations - a directed graph with real bigint FKs (ON DELETE CASCADE)
/// to ciir_documents on both source_document_id/target_document_id - to expand code-queries
/// matches into their surrounding relationship graph. See
/// .specs/05-code-queries-relationship-graph.md for the full design and the schema facts this
/// SQL relies on (both columns nullable; null means an external/unresolved reference).
/// </summary>
public sealed class RelationshipGraphRepository(NpgsqlDataSource dataSource) : IRelationshipGraphRepository
{
    public async Task<CodeGraph> GetGraphAsync(
        long projectId,
        IReadOnlyList<long> rootIds,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // Roots are the code-queries matches themselves - already fully described in the
        // `matches` part of the response, so they're excluded from `graph.nodes` to avoid
        // duplication, but still tracked here (depth 0) since edges touching them need a depth.
        var rootDepths = rootIds.Distinct().ToDictionary(id => id, _ => 0);
        var discovered = await FindReachableNodeDepthsAsync(connection, projectId, rootIds, maxDepth, maxNodes, cancellationToken);

        var truncated = discovered.Count > maxNodes;
        if (truncated)
        {
            discovered = discovered
                .OrderBy(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Take(maxNodes)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        var depthByNodeId = new Dictionary<long, int>(rootDepths);
        foreach (var (nodeId, depth) in discovered)
        {
            depthByNodeId[nodeId] = depth;
        }

        var discoveredIds = discovered.Keys.ToArray();
        var allIds = depthByNodeId.Keys.ToArray();
        var nodes = await FetchNodesAsync(connection, discoveredIds, depthByNodeId, cancellationToken);
        var edges = await FetchEdgesAsync(connection, projectId, allIds, depthByNodeId, cancellationToken);

        return new CodeGraph(nodes, edges, truncated);
    }

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<MatchRelation>>> GetDirectRelationsAsync(
        long projectId,
        IReadOnlyList<long> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0)
        {
            return new Dictionary<long, IReadOnlyList<MatchRelation>>();
        }

        var ids = documentIds.ToArray();

        // Unlike FetchEdgesAsync (which restricts both endpoints to an already-resolved node
        // set), this only requires ONE endpoint to be one of the requested ids - it's meant to
        // return a match's complete 1-hop neighborhood, not a subset of some other bounded graph.
        const string sql = """
            SELECT source_document_id AS FromId
                 , target_document_id AS ToId
                 , kind AS RelationType
                 , target_symbol AS TargetSymbol
                 , resolution_origin AS ResolutionOrigin
            FROM public.ciir_relations
            WHERE project_id = @ProjectId
              AND (source_document_id = ANY(@Ids) OR target_document_id = ANY(@Ids))
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProjectId = projectId, Ids = ids }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<GraphEdgeProjection>(command);

        var requestedIds = ids.ToHashSet();

        // A relation between two of the requested ids (e.g. match A calls match B, both
        // present in the same page of results) is attributed to BOTH A and B - it is, in fact,
        // a direct relation of each of them.
        return rows
            .SelectMany(row => TouchedRequestedIds(row, requestedIds).Select(id => (Id: id, Relation: row.ToMatchRelation())))
            .GroupBy(pair => pair.Id)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<MatchRelation>)group.Select(pair => pair.Relation).ToList());
    }

    private static IEnumerable<long> TouchedRequestedIds(GraphEdgeProjection row, HashSet<long> requestedIds)
    {
        if (row.FromId is { } fromId && requestedIds.Contains(fromId))
        {
            yield return fromId;
        }

        if (row.ToId is { } toId && toId != row.FromId && requestedIds.Contains(toId))
        {
            yield return toId;
        }
    }

    private static async Task<Dictionary<long, int>> FindReachableNodeDepthsAsync(
        NpgsqlConnection connection,
        long projectId,
        IReadOnlyList<long> rootIds,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH RECURSIVE root_ids AS (
                SELECT unnest(@RootIds::bigint[]) AS node_id
            ),
            graph(node_id, depth, path) AS (
                SELECT node_id, 0, ARRAY[node_id] FROM root_ids

                UNION ALL

                SELECT
                    CASE WHEN r.source_document_id = g.node_id THEN r.target_document_id ELSE r.source_document_id END,
                    g.depth + 1,
                    g.path || CASE WHEN r.source_document_id = g.node_id THEN r.target_document_id ELSE r.source_document_id END
                FROM graph g
                JOIN public.ciir_relations r
                  ON r.project_id = @ProjectId
                 AND (r.source_document_id = g.node_id OR r.target_document_id = g.node_id)
                WHERE g.depth < @MaxDepth
                  AND r.source_document_id IS NOT NULL
                  AND r.target_document_id IS NOT NULL
                  AND NOT (CASE WHEN r.source_document_id = g.node_id THEN r.target_document_id ELSE r.source_document_id END = ANY(g.path))
            )
            SELECT node_id AS NodeId, MIN(depth) AS Depth
            FROM graph
            WHERE depth > 0
              AND node_id <> ALL(@RootIds::bigint[])
            GROUP BY node_id
            ORDER BY Depth, NodeId
            LIMIT @Limit
            """;

        var parameters = new
        {
            RootIds = rootIds.ToArray(),
            ProjectId = projectId,
            MaxDepth = maxDepth,

            // Fetch one extra row beyond maxNodes purely to detect truncation (distinguishes
            // "exactly maxNodes reachable" from "more than maxNodes reachable, cut off").
            Limit = maxNodes + 1,
        };

        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<(long NodeId, int Depth)>(command);
        return rows.ToDictionary(r => r.NodeId, r => r.Depth);
    }

    private static async Task<IReadOnlyList<GraphNode>> FetchNodesAsync(
        NpgsqlConnection connection,
        long[] ids,
        IReadOnlyDictionary<long, int> depthByNodeId,
        CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return [];
        }

        const string sql = """
            SELECT id AS Id
                 , kind AS Kind
                 , symbol_container AS SymbolContainer
                 , symbol_name AS SymbolName
                 , symbol_qualified_name AS SymbolQualifiedName
                 , symbol_canonical_name AS SymbolCanonicalName
                 , source_path AS SourceFile
            FROM public.ciir_documents
            WHERE id = ANY(@Ids)
            """;

        var command = new CommandDefinition(sql, new { Ids = ids }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<GraphNodeProjection>(command);
        return rows.Select(r => r.ToNode(depthByNodeId[r.Id])).ToList();
    }

    private static async Task<IReadOnlyList<GraphEdge>> FetchEdgesAsync(
        NpgsqlConnection connection,
        long projectId,
        long[] ids,
        IReadOnlyDictionary<long, int> depthByNodeId,
        CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return [];
        }

        // source_document_id is, in practice, always populated (the origin of a relation is
        // always something indexed); target_document_id is null for external/unresolved
        // references. Restricting source to the reached set (and target to it-or-null) keeps
        // every edge's known endpoint(s) within the graph this call already resolved, including
        // when maxNodes truncation excluded a would-be neighbor.
        const string sql = """
            SELECT source_document_id AS FromId
                 , target_document_id AS ToId
                 , kind AS RelationType
                 , target_symbol AS TargetSymbol
                 , resolution_origin AS ResolutionOrigin
            FROM public.ciir_relations
            WHERE project_id = @ProjectId
              AND source_document_id = ANY(@Ids)
              AND (target_document_id = ANY(@Ids) OR target_document_id IS NULL)
            """;

        var command = new CommandDefinition(sql, new { ProjectId = projectId, Ids = ids }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<GraphEdgeProjection>(command);
        return rows.Select(r => r.ToEdge(depthByNodeId)).ToList();
    }

    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties - the standard .NET convention is PascalCase, matching the "AS Id",
    // "AS Kind", ... aliases in the SQL above that Dapper binds them from.
#pragma warning disable SA1313
    private sealed record GraphNodeProjection(
        long Id,
        string Kind,
        string? SymbolContainer,
        string? SymbolName,
        string? SymbolQualifiedName,
        string? SymbolCanonicalName,
        string? SourceFile)
    {
        public GraphNode ToNode(int depth) => new(Id, Kind, SymbolContainer, SymbolName, SymbolQualifiedName, SymbolCanonicalName, SourceFile, depth);
    }

    private sealed record GraphEdgeProjection(long? FromId, long? ToId, string RelationType, string TargetSymbol, string ResolutionOrigin)
    {
        public GraphEdge ToEdge(IReadOnlyDictionary<long, int> depthByNodeId)
        {
            var fromDepth = FromId.HasValue && depthByNodeId.TryGetValue(FromId.Value, out var fd) ? fd : (int?)null;
            var toDepth = ToId.HasValue && depthByNodeId.TryGetValue(ToId.Value, out var td) ? td : (int?)null;
            var depth = new[] { fromDepth, toDepth }.Where(d => d.HasValue).Select(d => d!.Value).DefaultIfEmpty(0).Min();

            return new GraphEdge(FromId, ToId, RelationType, TargetSymbol, ResolutionOrigin, depth);
        }

        public MatchRelation ToMatchRelation() => new(FromId, ToId, RelationType, TargetSymbol, ResolutionOrigin);
    }
#pragma warning restore SA1313
}
