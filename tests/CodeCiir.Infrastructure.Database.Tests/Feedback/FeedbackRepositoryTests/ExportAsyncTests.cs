using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.Feedback.FeedbackRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class ExportAsyncTests(PostgresFixture fixture) : BaseFeedbackRepositoryTests(fixture)
{
    [Fact]
    public async Task Should_ReturnRowsOrderedByCreatedAtAscending_When_FeedbackIsWithinTheWindow()
    {
        var projectId = await Seeder.InsertProjectAsync();
        await Seeder.InsertFeedbackAtAsync(projectId, Utc(2024, 4, 10, 12), useful: false);
        await Seeder.InsertFeedbackAtAsync(projectId, Utc(2024, 4, 5, 12), useful: true);

        var rows = await Sut.ExportAsync(Utc(2024, 4, 1), Utc(2024, 4, 30, 23, 59, 59), projectId);

        rows.Select(r => r.CreatedAt).ShouldBe([Utc(2024, 4, 5, 12), Utc(2024, 4, 10, 12)]);
    }

    [Fact]
    public async Task Should_MapEveryColumnOfTheRow_When_FeedbackIsExported()
    {
        var projectName = $"proj-{Guid.NewGuid():N}";
        var publicId = Guid.NewGuid();
        var projectId = await Seeder.InsertProjectAsync(projectName, publicId: publicId);
        double[] similarities = [0.91, 0.73];
        await Seeder.InsertFeedbackAtAsync(projectId, Utc(2024, 6, 10, 12), useful: false, similarities: similarities, reason: "not related");

        var rows = await Sut.ExportAsync(Utc(2024, 6, 1), Utc(2024, 6, 30, 23, 59, 59), projectId);

        var row = rows.ShouldHaveSingleItem();
        (row.ProjectId, row.ProjectName, row.Useful, row.Reason, row.Username).ShouldBe((publicId, projectName, false, "not related", "tester"));
        row.Similarities.ShouldBe(similarities);
    }

    [Fact]
    public async Task Should_ReturnEmptySimilaritiesAndNullReason_When_FeedbackHasNeither()
    {
        var projectId = await Seeder.InsertProjectAsync();
        await Seeder.InsertFeedbackAtAsync(projectId, Utc(2024, 5, 5, 12), useful: true, similarities: [], reason: null);

        var rows = await Sut.ExportAsync(Utc(2024, 5, 1), Utc(2024, 5, 31, 23, 59, 59), projectId);

        var row = rows.ShouldHaveSingleItem();
        row.Similarities.ShouldBeEmpty();
        row.Reason.ShouldBeNull();
    }

    [Fact]
    public async Task Should_ExcludeFeedback_When_CreatedAtIsOutsideTheWindow()
    {
        var projectId = await Seeder.InsertProjectAsync();
        await Seeder.InsertFeedbackAtAsync(projectId, Utc(2021, 7, 1, 12), useful: true);
        await Seeder.InsertFeedbackAtAsync(projectId, Utc(2021, 7, 15, 12), useful: true);

        var rows = await Sut.ExportAsync(Utc(2021, 7, 10), Utc(2021, 7, 20, 23, 59, 59), projectId);

        rows.Select(r => r.CreatedAt).ShouldBe([Utc(2021, 7, 15, 12)]);
    }

    [Fact]
    public async Task Should_RestrictToASingleProject_When_ProjectIdIsGiven()
    {
        var filteredPublicId = Guid.NewGuid();
        var filteredProjectId = await Seeder.InsertProjectAsync(publicId: filteredPublicId);
        var otherProjectId = await Seeder.InsertProjectAsync();
        await Seeder.InsertFeedbackAtAsync(filteredProjectId, Utc(2022, 8, 4, 12), useful: true);
        await Seeder.InsertFeedbackAtAsync(otherProjectId, Utc(2022, 8, 4, 12), useful: false);

        var rows = await Sut.ExportAsync(Utc(2022, 8, 1), Utc(2022, 8, 31, 23, 59, 59), filteredProjectId);

        rows.Select(r => r.ProjectId).ShouldBe([filteredPublicId]);
    }

    [Fact]
    public async Task Should_IncludeEveryRegisteredProject_When_NoProjectIdIsGiven()
    {
        var firstPublicId = Guid.NewGuid();
        var secondPublicId = Guid.NewGuid();
        var firstProjectId = await Seeder.InsertProjectAsync(publicId: firstPublicId);
        var secondProjectId = await Seeder.InsertProjectAsync(publicId: secondPublicId);
        await Seeder.InsertFeedbackAtAsync(firstProjectId, Utc(2023, 10, 4, 12), useful: true);
        await Seeder.InsertFeedbackAtAsync(secondProjectId, Utc(2023, 10, 4, 13), useful: false);

        var rows = await Sut.ExportAsync(Utc(2023, 10, 1), Utc(2023, 10, 31, 23, 59, 59), null);

        new[] { firstPublicId, secondPublicId }.ShouldBeSubsetOf(rows.Select(r => r.ProjectId).ToList());
    }
}
