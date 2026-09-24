using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.CodeQueries.RelationshipGraphRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class GetDirectRelationsAsyncTests(PostgresFixture fixture) : BaseRelationshipGraphRepositoryTests(fixture)
{
    [Fact]
    public async Task Should_ReturnAnEmptyDictionary_When_DocumentHasNoRelations()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var id = await Seeder.InsertDocumentAsync(projectId, "Solo");

        var relations = await Sut.GetDirectRelationsAsync(projectId, [id]);

        relations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_AttributeTheRelationToTheSource_When_RelationIsOutgoing()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var callerId = await Seeder.InsertDocumentAsync(projectId, "Caller");
        var calleeId = await Seeder.InsertDocumentAsync(projectId, "Callee");
        await Seeder.InsertRelationAsync(projectId, callerId, calleeId, "calls");

        var relations = await Sut.GetDirectRelationsAsync(projectId, [callerId]);

        relations[callerId].Select(r => (r.FromId, r.ToId, r.RelationType)).ShouldBe([(callerId, calleeId, "calls")]);
    }

    [Fact]
    public async Task Should_AttributeTheRelationToTheTarget_When_RelationIsIncoming()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var callerId = await Seeder.InsertDocumentAsync(projectId, "Caller");
        var calleeId = await Seeder.InsertDocumentAsync(projectId, "Callee");
        await Seeder.InsertRelationAsync(projectId, callerId, calleeId, "calls");

        var relations = await Sut.GetDirectRelationsAsync(projectId, [calleeId]);

        relations[calleeId].Select(r => (r.FromId, r.ToId)).ShouldBe([(callerId, calleeId)]);
    }

    [Fact]
    public async Task Should_AttributeTheRelationToBoth_When_RelationIsBetweenTwoRequestedIds()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var firstId = await Seeder.InsertDocumentAsync(projectId, "A");
        var secondId = await Seeder.InsertDocumentAsync(projectId, "B");
        await Seeder.InsertRelationAsync(projectId, firstId, secondId, "calls");

        var relations = await Sut.GetDirectRelationsAsync(projectId, [firstId, secondId]);

        (relations[firstId].Count, relations[secondId].Count).ShouldBe((1, 1));
    }

    [Fact]
    public async Task Should_NotIncludeTwoHopRelations_When_ChainIsLongerThanOneHop()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        var childId = await Seeder.InsertDocumentAsync(projectId, "Child");
        var grandchildId = await Seeder.InsertDocumentAsync(projectId, "Grandchild");
        await Seeder.InsertRelationAsync(projectId, rootId, childId, "calls");
        await Seeder.InsertRelationAsync(projectId, childId, grandchildId, "calls");

        var relations = await Sut.GetDirectRelationsAsync(projectId, [rootId]);

        relations[rootId].Select(r => r.ToId).ShouldBe([childId]);
    }

    [Fact]
    public async Task Should_IncludeTheRelation_When_TargetIsUnresolvedAndExternal()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var rootId = await Seeder.InsertDocumentAsync(projectId, "Root");
        await Seeder.InsertRelationAsync(projectId, rootId, targetDocumentId: null, kind: "calls", targetSymbol: "System.String.Format", resolutionStatus: "external", resolutionOrigin: "framework");

        var relations = await Sut.GetDirectRelationsAsync(projectId, [rootId]);

        relations[rootId].Select(r => (r.ToId, r.TargetSymbol)).ShouldBe([((long?)null, "System.String.Format")]);
    }

    [Fact]
    public async Task Should_ReturnAnEmptyDictionary_When_NoDocumentIdsAreGiven()
    {
        var projectId = await Seeder.InsertProjectAsync();

        var relations = await Sut.GetDirectRelationsAsync(projectId, []);

        relations.ShouldBeEmpty();
    }
}
