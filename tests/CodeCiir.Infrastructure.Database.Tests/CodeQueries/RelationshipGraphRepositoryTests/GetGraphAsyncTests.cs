using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.CodeQueries.RelationshipGraphRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class GetGraphAsyncTests(PostgresFixture fixture) : BaseRelationshipGraphRepositoryTests(fixture)
{
    private const int MaxDepth = 2;
    private const int MaxNodes = 100;

    [Fact]
    public async Task Should_ReturnAnEmptyGraph_When_RootHasNoRelations()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, MaxNodes);

        (graph.Nodes.Count, graph.Edges.Count, graph.Truncated).ShouldBe((0, 0, false));
    }

    [Fact]
    public async Task Should_ReturnTheDirectlyRelatedNodeAtDepthOne_When_RootHasOneRelation()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        var calleeId = await Seeder.InsertDocumentAsync(projectId, "Callee");
        await Seeder.InsertRelationAsync(projectId, rootId, calleeId, "calls");

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, MaxNodes);

        graph.Nodes.Select(n => (n.Id, n.Depth)).ShouldBe([(calleeId, 1)]);
    }

    [Fact]
    public async Task Should_ReturnTheEdgeBetweenRootAndCallee_When_RootHasOneRelation()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        var calleeId = await Seeder.InsertDocumentAsync(projectId, "Callee");
        await Seeder.InsertRelationAsync(projectId, rootId, calleeId, "calls");

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, MaxNodes);

        graph.Edges.Select(e => (e.FromId, e.ToId, e.RelationType)).ShouldBe([(rootId, calleeId, "calls")]);
    }

    [Fact]
    public async Task Should_ReturnTheCaller_When_RelationPointsAtTheRoot()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        var callerId = await Seeder.InsertDocumentAsync(projectId, "Caller");
        await Seeder.InsertRelationAsync(projectId, callerId, rootId, "calls");

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, MaxNodes);

        graph.Nodes.Select(n => n.Id).ShouldBe([callerId]);
    }

    [Fact]
    public async Task Should_ReachTheGrandchildAtDepthTwo_When_TwoHopsAreAllowed()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        var childId = await Seeder.InsertDocumentAsync(projectId, "Child");
        var grandchildId = await Seeder.InsertDocumentAsync(projectId, "Grandchild");
        await Seeder.InsertRelationAsync(projectId, rootId, childId, "calls");
        await Seeder.InsertRelationAsync(projectId, childId, grandchildId, "calls");

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, MaxNodes);

        graph.Nodes.Select(n => (n.Id, n.Depth)).ShouldBe([(childId, 1), (grandchildId, 2)], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_NotReachNodesBeyondMaxDepth_When_ChainIsLongerThanMaxDepth()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        var childId = await Seeder.InsertDocumentAsync(projectId, "Child");
        var grandchildId = await Seeder.InsertDocumentAsync(projectId, "Grandchild");
        var greatGrandchildId = await Seeder.InsertDocumentAsync(projectId, "GreatGrandchild");
        await Seeder.InsertRelationAsync(projectId, rootId, childId, "calls");
        await Seeder.InsertRelationAsync(projectId, childId, grandchildId, "calls");
        await Seeder.InsertRelationAsync(projectId, grandchildId, greatGrandchildId, "calls");

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, MaxNodes);

        graph.Nodes.ShouldNotContain(n => n.Id == greatGrandchildId);
    }

    [Fact]
    public async Task Should_TerminateAndListTheNodeOnce_When_RelationsFormACycle()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        var otherId = await Seeder.InsertDocumentAsync(projectId, "Other");
        await Seeder.InsertRelationAsync(projectId, rootId, otherId, "calls");
        await Seeder.InsertRelationAsync(projectId, otherId, rootId, "calls");

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, MaxNodes);

        graph.Nodes.Select(n => n.Id).ShouldBe([otherId]);
    }

    [Fact]
    public async Task Should_ReturnALeafEdgeWithoutANode_When_TargetIsUnresolvedAndExternal()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        await Seeder.InsertRelationAsync(projectId, rootId, targetDocumentId: null, kind: "calls", targetSymbol: "System.String.Format", resolutionStatus: "external", resolutionOrigin: "framework");

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, MaxNodes);

        graph.Nodes.ShouldBeEmpty();
        graph.Edges.Select(e => (e.ToId, e.TargetSymbol, e.ResolutionOrigin)).ShouldBe([((long?)null, "System.String.Format", "framework")]);
    }

    [Fact]
    public async Task Should_ReturnEveryRelationKind_When_RootHasOneRelationOfEachKind()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        string[] kinds = ["calls", "reads", "constructs", "overrides", "writes", "implements", "throws", "inherits", "catches"];
        foreach (var kind in kinds)
        {
            var targetId = await Seeder.InsertDocumentAsync(projectId, $"Target-{kind}");
            await Seeder.InsertRelationAsync(projectId, rootId, targetId, kind);
        }

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, MaxNodes);

        graph.Edges.Select(e => e.RelationType).ShouldBe(kinds, ignoreOrder: true);
    }

    [Fact]
    public async Task Should_TruncateAndSignalIt_When_FanOutExceedsMaxNodes()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        for (var i = 0; i < 5; i++)
        {
            var targetId = await Seeder.InsertDocumentAsync(projectId, $"Target-{i}");
            await Seeder.InsertRelationAsync(projectId, rootId, targetId, "calls");
        }

        var graph = await Sut.GetGraphAsync(projectId, [rootId], MaxDepth, maxNodes: 3);

        (graph.Nodes.Count, graph.Truncated).ShouldBe((3, true));
    }
}
