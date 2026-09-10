using CodeCiir.Infrastructure.Database.CodeQueries;
using Dapper;
using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.CodeQueries;

[Collection(PostgresCollection.Name)]
public sealed class RelationshipGraphRepositoryTests(PostgresFixture fixture)
{
    private readonly RelationshipGraphRepository _sut = new(fixture.DataSource);

    [Fact]
    public async Task GetGraphAsync_NoRelations_ReturnsEmptyGraph()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");

        var graph = await _sut.GetGraphAsync(projectId, [rootId], maxDepth: 2, maxNodes: 100);

        graph.Nodes.ShouldBeEmpty();
        graph.Edges.ShouldBeEmpty();
        graph.Truncated.ShouldBeFalse();
    }

    [Fact]
    public async Task GetGraphAsync_OneHop_ReturnsDirectlyRelatedNode()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        var calleeId = await InsertDocumentAsync(projectId, "Callee");
        await InsertRelationAsync(projectId, rootId, calleeId, "calls");

        var graph = await _sut.GetGraphAsync(projectId, [rootId], maxDepth: 2, maxNodes: 100);

        graph.Nodes.ShouldHaveSingleItem();
        graph.Nodes[0].Id.ShouldBe(calleeId);
        graph.Nodes[0].Depth.ShouldBe(1);
        graph.Edges.ShouldHaveSingleItem();
        graph.Edges[0].FromId.ShouldBe(rootId);
        graph.Edges[0].ToId.ShouldBe(calleeId);
        graph.Edges[0].RelationType.ShouldBe("calls");
    }

    [Fact]
    public async Task GetGraphAsync_ReverseDirection_ReturnsCallerAsWellAsCallee()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        var callerId = await InsertDocumentAsync(projectId, "Caller");
        await InsertRelationAsync(projectId, callerId, rootId, "calls");

        var graph = await _sut.GetGraphAsync(projectId, [rootId], maxDepth: 2, maxNodes: 100);

        graph.Nodes.ShouldContain(n => n.Id == callerId);
    }

    [Fact]
    public async Task GetGraphAsync_TwoHops_ReachesGrandchild()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        var childId = await InsertDocumentAsync(projectId, "Child");
        var grandchildId = await InsertDocumentAsync(projectId, "Grandchild");
        await InsertRelationAsync(projectId, rootId, childId, "calls");
        await InsertRelationAsync(projectId, childId, grandchildId, "calls");

        var graph = await _sut.GetGraphAsync(projectId, [rootId], maxDepth: 2, maxNodes: 100);

        graph.Nodes.Select(n => n.Id).ShouldBe([childId, grandchildId], ignoreOrder: true);
        graph.Nodes.Single(n => n.Id == grandchildId).Depth.ShouldBe(2);
    }

    [Fact]
    public async Task GetGraphAsync_BeyondMaxDepth_IsNotReached()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        var childId = await InsertDocumentAsync(projectId, "Child");
        var grandchildId = await InsertDocumentAsync(projectId, "Grandchild");
        var greatGrandchildId = await InsertDocumentAsync(projectId, "GreatGrandchild");
        await InsertRelationAsync(projectId, rootId, childId, "calls");
        await InsertRelationAsync(projectId, childId, grandchildId, "calls");
        await InsertRelationAsync(projectId, grandchildId, greatGrandchildId, "calls");

        var graph = await _sut.GetGraphAsync(projectId, [rootId], maxDepth: 2, maxNodes: 100);

        graph.Nodes.ShouldNotContain(n => n.Id == greatGrandchildId);
    }

    [Fact]
    public async Task GetGraphAsync_DirectCycle_DoesNotLoopForeverAndDeduplicatesNode()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        var otherId = await InsertDocumentAsync(projectId, "Other");
        await InsertRelationAsync(projectId, rootId, otherId, "calls");
        await InsertRelationAsync(projectId, otherId, rootId, "calls");

        var graph = await _sut.GetGraphAsync(projectId, [rootId], maxDepth: 2, maxNodes: 100);

        graph.Nodes.Count(n => n.Id == otherId).ShouldBe(1);
    }

    [Fact]
    public async Task GetGraphAsync_UnresolvedExternalTarget_AppearsAsLeafEdgeWithoutNode()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        await InsertRelationAsync(projectId, rootId, targetDocumentId: null, kind: "calls", targetSymbol: "System.String.Format", resolutionStatus: "external", resolutionOrigin: "framework");

        var graph = await _sut.GetGraphAsync(projectId, [rootId], maxDepth: 2, maxNodes: 100);

        graph.Nodes.ShouldBeEmpty();
        graph.Edges.ShouldHaveSingleItem();
        graph.Edges[0].ToId.ShouldBeNull();
        graph.Edges[0].TargetSymbol.ShouldBe("System.String.Format");
        graph.Edges[0].ResolutionOrigin.ShouldBe("framework");
    }

    [Fact]
    public async Task GetGraphAsync_AllRelationKindsPresent_AreAllReturned()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        var kinds = new[] { "calls", "reads", "constructs", "overrides", "writes", "implements", "throws", "inherits", "catches" };
        foreach (var kind in kinds)
        {
            var targetId = await InsertDocumentAsync(projectId, $"Target-{kind}");
            await InsertRelationAsync(projectId, rootId, targetId, kind);
        }

        var graph = await _sut.GetGraphAsync(projectId, [rootId], maxDepth: 2, maxNodes: 100);

        graph.Edges.Select(e => e.RelationType).ShouldBe(kinds, ignoreOrder: true);
    }

    [Fact]
    public async Task GetGraphAsync_FanOutAboveMaxNodes_TruncatesAndSignals()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        for (var i = 0; i < 5; i++)
        {
            var targetId = await InsertDocumentAsync(projectId, $"Target-{i}");
            await InsertRelationAsync(projectId, rootId, targetId, "calls");
        }

        var graph = await _sut.GetGraphAsync(projectId, [rootId], maxDepth: 2, maxNodes: 3);

        graph.Nodes.Count.ShouldBe(3);
        graph.Truncated.ShouldBeTrue();
    }

    [Fact]
    public async Task GetDirectRelationsAsync_NoRelations_ReturnsEmptyDictionary()
    {
        var projectId = await InsertProjectAsync();
        var id = await InsertDocumentAsync(projectId, "Solo");

        var relations = await _sut.GetDirectRelationsAsync(projectId, [id]);

        relations.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDirectRelationsAsync_OutgoingRelation_IsAttributedToSource()
    {
        var projectId = await InsertProjectAsync();
        var callerId = await InsertDocumentAsync(projectId, "Caller");
        var calleeId = await InsertDocumentAsync(projectId, "Callee");
        await InsertRelationAsync(projectId, callerId, calleeId, "calls");

        var relations = await _sut.GetDirectRelationsAsync(projectId, [callerId]);

        relations[callerId].ShouldHaveSingleItem();
        relations[callerId][0].FromId.ShouldBe(callerId);
        relations[callerId][0].ToId.ShouldBe(calleeId);
        relations[callerId][0].RelationType.ShouldBe("calls");
    }

    [Fact]
    public async Task GetDirectRelationsAsync_IncomingRelation_IsAttributedToTarget()
    {
        var projectId = await InsertProjectAsync();
        var callerId = await InsertDocumentAsync(projectId, "Caller");
        var calleeId = await InsertDocumentAsync(projectId, "Callee");
        await InsertRelationAsync(projectId, callerId, calleeId, "calls");

        var relations = await _sut.GetDirectRelationsAsync(projectId, [calleeId]);

        relations[calleeId].ShouldHaveSingleItem();
        relations[calleeId][0].FromId.ShouldBe(callerId);
        relations[calleeId][0].ToId.ShouldBe(calleeId);
    }

    [Fact]
    public async Task GetDirectRelationsAsync_RelationBetweenTwoRequestedIds_IsAttributedToBoth()
    {
        var projectId = await InsertProjectAsync();
        var aId = await InsertDocumentAsync(projectId, "A");
        var bId = await InsertDocumentAsync(projectId, "B");
        await InsertRelationAsync(projectId, aId, bId, "calls");

        var relations = await _sut.GetDirectRelationsAsync(projectId, [aId, bId]);

        relations[aId].ShouldHaveSingleItem();
        relations[bId].ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetDirectRelationsAsync_TwoHopRelation_IsNotIncluded()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        var childId = await InsertDocumentAsync(projectId, "Child");
        var grandchildId = await InsertDocumentAsync(projectId, "Grandchild");
        await InsertRelationAsync(projectId, rootId, childId, "calls");
        await InsertRelationAsync(projectId, childId, grandchildId, "calls");

        var relations = await _sut.GetDirectRelationsAsync(projectId, [rootId]);

        relations[rootId].ShouldHaveSingleItem();
        relations[rootId][0].ToId.ShouldBe(childId);
    }

    [Fact]
    public async Task GetDirectRelationsAsync_UnresolvedExternalTarget_IsIncluded()
    {
        var projectId = await InsertProjectAsync();
        var rootId = await InsertDocumentAsync(projectId, "Root");
        await InsertRelationAsync(projectId, rootId, targetDocumentId: null, kind: "calls", targetSymbol: "System.String.Format", resolutionStatus: "external", resolutionOrigin: "framework");

        var relations = await _sut.GetDirectRelationsAsync(projectId, [rootId]);

        relations[rootId].ShouldHaveSingleItem();
        relations[rootId][0].ToId.ShouldBeNull();
        relations[rootId][0].TargetSymbol.ShouldBe("System.String.Format");
    }

    [Fact]
    public async Task GetDirectRelationsAsync_EmptyDocumentIds_ReturnsEmptyDictionary()
    {
        var projectId = await InsertProjectAsync();

        var relations = await _sut.GetDirectRelationsAsync(projectId, []);

        relations.ShouldBeEmpty();
    }

    private async Task<long> InsertProjectAsync()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO public.projects (name, embedding_model, embedding_dimensions)
            VALUES (@Name, 'test-model', 3)
            RETURNING id
            """,
            new { Name = $"proj-{Guid.NewGuid():N}" });
    }

    private async Task<long> InsertDocumentAsync(long projectId, string symbolName)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO public.ciir_documents (project_id, ciir_id, schema_version, kind, language, symbol_name, content)
            VALUES (@ProjectId, @CiirId, '1.0', 'method', 'csharp', @SymbolName, '{}'::jsonb)
            RETURNING id
            """,
            new { ProjectId = projectId, CiirId = $"sha256:{Guid.NewGuid():N}", SymbolName = symbolName });
    }

    private async Task InsertRelationAsync(
        long projectId,
        long sourceDocumentId,
        long? targetDocumentId,
        string kind,
        string targetSymbol = "target",
        string resolutionStatus = "resolved",
        string resolutionOrigin = "project")
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.ciir_relations
                (project_id, source_ciir_id, source_document_id, target_document_id, kind, target_symbol,
                 resolution_status, resolution_origin, idempotency_key)
            VALUES
                (@ProjectId, @SourceCiirId, @SourceDocumentId, @TargetDocumentId, @Kind, @TargetSymbol,
                 @ResolutionStatus, @ResolutionOrigin, @IdempotencyKey)
            """,
            new
            {
                ProjectId = projectId,
                SourceCiirId = $"sha256:{Guid.NewGuid():N}",
                SourceDocumentId = sourceDocumentId,
                TargetDocumentId = targetDocumentId,
                Kind = kind,
                TargetSymbol = targetSymbol,
                ResolutionStatus = resolutionStatus,
                ResolutionOrigin = resolutionOrigin,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
            });
    }
}
