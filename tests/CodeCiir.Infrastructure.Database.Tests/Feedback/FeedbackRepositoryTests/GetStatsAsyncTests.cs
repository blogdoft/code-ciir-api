using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.Feedback.FeedbackRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class GetStatsAsyncTests(PostgresFixture fixture) : BaseFeedbackRepositoryTests(fixture)
{
    // Week 1: Mon 2020-01-06 - Sun 2020-01-12. Week 2 (2020-01-13 - 01-19): no feedback at all.
    // Week 3: Mon 2020-01-20 - Sun 2020-01-26.
    private static readonly DateTime GridStart = Utc(2020, 1, 6);
    private static readonly DateTime GridEnd = Utc(2020, 1, 26, 23, 59, 59);

    [Fact]
    public async Task Should_ReturnADenseGridWithEveryWeekOfTheWindow_When_FeedbackSkipsAWeek()
    {
        var (internalId, _) = await SeedSparseFeedbackAsync();

        var weeks = await Sut.GetStatsAsync(GridStart, GridEnd, internalId);

        weeks.Select(w => (w.WeekStart, w.WeekEnd)).ShouldBe(
        [
            (new DateOnly(2020, 1, 6), new DateOnly(2020, 1, 12)),
            (new DateOnly(2020, 1, 13), new DateOnly(2020, 1, 19)),
            (new DateOnly(2020, 1, 20), new DateOnly(2020, 1, 26)),
        ]);
    }

    [Fact]
    public async Task Should_CountAndPercentUsefulVersusNotUseful_When_WeekHasFeedback()
    {
        var (internalId, publicId) = await SeedSparseFeedbackAsync();

        var weeks = await Sut.GetStatsAsync(GridStart, GridEnd, internalId);

        var stats = weeks.Single(w => w.WeekStart == new DateOnly(2020, 1, 6)).Projects.Single(p => p.ProjectId == publicId);
        (stats.TotalCount, stats.UsefulCount, stats.NotUsefulCount).ShouldBe((2, 1, 1));
        (stats.UsefulPercentage, stats.NotUsefulPercentage).ShouldBe((50, 50));
    }

    [Fact]
    public async Task Should_ZeroFillTheWeek_When_WeekHasNoFeedback()
    {
        var (internalId, publicId) = await SeedSparseFeedbackAsync();

        var weeks = await Sut.GetStatsAsync(GridStart, GridEnd, internalId);

        var stats = weeks.Single(w => w.WeekStart == new DateOnly(2020, 1, 13)).Projects.Single(p => p.ProjectId == publicId);
        (stats.TotalCount, stats.UsefulPercentage, stats.NotUsefulPercentage).ShouldBe((0, 0, 0));
    }

    [Fact]
    public async Task Should_ExcludeFeedback_When_CreatedAtIsOutsideTheWindow()
    {
        var publicId = Guid.NewGuid();
        var internalId = await Seeder.InsertProjectAsync(publicId: publicId);
        await Seeder.InsertFeedbackAtAsync(internalId, Utc(2021, 3, 1, 12), useful: true);
        await Seeder.InsertFeedbackAtAsync(internalId, Utc(2021, 3, 22, 12), useful: true);
        await Seeder.InsertFeedbackAtAsync(internalId, Utc(2021, 3, 10, 12), useful: true);

        var weeks = await Sut.GetStatsAsync(Utc(2021, 3, 8), Utc(2021, 3, 14, 23, 59, 59), internalId);

        weeks.Single().Projects.Single(p => p.ProjectId == publicId).TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Should_RestrictToASingleProject_When_ProjectIdIsGiven()
    {
        var filteredPublicId = Guid.NewGuid();
        var filteredInternalId = await Seeder.InsertProjectAsync(publicId: filteredPublicId);
        var otherInternalId = await Seeder.InsertProjectAsync();
        await Seeder.InsertFeedbackAtAsync(filteredInternalId, Utc(2022, 5, 4, 12), useful: true);
        await Seeder.InsertFeedbackAtAsync(otherInternalId, Utc(2022, 5, 4, 12), useful: false);

        var weeks = await Sut.GetStatsAsync(Utc(2022, 5, 2), Utc(2022, 5, 8, 23, 59, 59), filteredInternalId);

        weeks.Single().Projects.Select(p => (p.ProjectId, p.TotalCount, p.UsefulCount)).ShouldBe([(filteredPublicId, 1L, 1L)]);
    }

    [Fact]
    public async Task Should_IncludeEveryRegisteredProject_When_NoProjectIdIsGiven()
    {
        var publicIdWithFeedback = Guid.NewGuid();
        var publicIdWithoutFeedback = Guid.NewGuid();
        var internalIdWithFeedback = await Seeder.InsertProjectAsync(publicId: publicIdWithFeedback);
        await Seeder.InsertProjectAsync(publicId: publicIdWithoutFeedback);
        await Seeder.InsertFeedbackAtAsync(internalIdWithFeedback, Utc(2023, 9, 4, 12), useful: true);

        var weeks = await Sut.GetStatsAsync(Utc(2023, 9, 4), Utc(2023, 9, 10, 23, 59, 59), null);

        var projects = weeks.Single().Projects;
        projects.ShouldContain(p => p.ProjectId == publicIdWithFeedback && p.TotalCount == 1);
        projects.ShouldContain(p => p.ProjectId == publicIdWithoutFeedback && p.TotalCount == 0);
    }

    private async Task<(long InternalId, Guid PublicId)> SeedSparseFeedbackAsync()
    {
        var publicId = Guid.NewGuid();
        var internalId = await Seeder.InsertProjectAsync(publicId: publicId);
        await Seeder.InsertFeedbackAtAsync(internalId, Utc(2020, 1, 6, 12), useful: true);
        await Seeder.InsertFeedbackAtAsync(internalId, Utc(2020, 1, 8, 12), useful: false);
        await Seeder.InsertFeedbackAtAsync(internalId, Utc(2020, 1, 20, 12), useful: true);
        return (internalId, publicId);
    }
}
