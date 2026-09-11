using CodeCiir.Infrastructure.Database.Feedback;
using Dapper;
using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.Feedback;

[Collection(PostgresCollection.Name)]
public sealed class FeedbackRepositoryTests(PostgresFixture fixture)
{
    private readonly FeedbackRepository _sut = new(fixture.DataSource);

    [Fact]
    public async Task InsertAsync_ValidFeedback_PersistsAndReturnsIt()
    {
        var projectId = await InsertProjectAsync();

        var result = await _sut.InsertAsync(projectId, "where is the retry logic?", true, [0.83, 0.71], null, "claude code");

        result.Id.ShouldBeGreaterThan(0);
        result.ProjectId.ShouldBe(projectId);
        result.Question.ShouldBe("where is the retry logic?");
        result.Useful.ShouldBeTrue();
        result.Similarities.ShouldBe([0.83, 0.71]);
        result.Reason.ShouldBeNull();
        result.User.ShouldBe("claude code");
        result.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task InsertAsync_EmptySimilarities_IsPersisted()
    {
        var projectId = await InsertProjectAsync();

        var result = await _sut.InsertAsync(projectId, "question", false, [], "not relevant", "codex");

        result.Similarities.ShouldBeEmpty();
        result.Reason.ShouldBe("not relevant");
    }

    [Fact]
    public async Task GetStatsAsync_FeedbackSkipsAWeek_ReturnsDenseWeeklyGrid()
    {
        var projectId = await InsertProjectAsync();

        // Week 1: Mon 2020-01-06 - Sun 2020-01-12. Week 2 (2020-01-13 - 01-19): no feedback at
        // all. Week 3: Mon 2020-01-20 - Sun 2020-01-26. Dates fixed far in the past so no other
        // test in this shared-container collection can pollute this window.
        await InsertFeedbackAtAsync(projectId, new DateTime(2020, 1, 6, 12, 0, 0, DateTimeKind.Utc), useful: true);
        await InsertFeedbackAtAsync(projectId, new DateTime(2020, 1, 8, 12, 0, 0, DateTimeKind.Utc), useful: false);
        await InsertFeedbackAtAsync(projectId, new DateTime(2020, 1, 20, 12, 0, 0, DateTimeKind.Utc), useful: true);

        var weeks = await _sut.GetStatsAsync(
            new DateTime(2020, 1, 6, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 1, 26, 23, 59, 59, DateTimeKind.Utc),
            projectId);

        weeks.Count.ShouldBe(3);

        var week1 = weeks.Single(w => w.WeekStart == new DateOnly(2020, 1, 6));
        week1.WeekEnd.ShouldBe(new DateOnly(2020, 1, 12));
        var week1Project = week1.Projects.Single(p => p.ProjectId == projectId);
        week1Project.TotalCount.ShouldBe(2);
        week1Project.UsefulCount.ShouldBe(1);
        week1Project.NotUsefulCount.ShouldBe(1);
        week1Project.UsefulPercentage.ShouldBe(50);
        week1Project.NotUsefulPercentage.ShouldBe(50);

        var week2 = weeks.Single(w => w.WeekStart == new DateOnly(2020, 1, 13));
        var week2Project = week2.Projects.Single(p => p.ProjectId == projectId);
        week2Project.TotalCount.ShouldBe(0);
        week2Project.UsefulPercentage.ShouldBe(0);
        week2Project.NotUsefulPercentage.ShouldBe(0);

        var week3 = weeks.Single(w => w.WeekStart == new DateOnly(2020, 1, 20));
        var week3Project = week3.Projects.Single(p => p.ProjectId == projectId);
        week3Project.TotalCount.ShouldBe(1);
        week3Project.UsefulCount.ShouldBe(1);
        week3Project.UsefulPercentage.ShouldBe(100);
    }

    [Fact]
    public async Task GetStatsAsync_CreatedAtOutsideWindow_IsExcluded()
    {
        var projectId = await InsertProjectAsync();
        await InsertFeedbackAtAsync(projectId, new DateTime(2021, 3, 1, 12, 0, 0, DateTimeKind.Utc), useful: true);
        await InsertFeedbackAtAsync(projectId, new DateTime(2021, 3, 22, 12, 0, 0, DateTimeKind.Utc), useful: true);
        await InsertFeedbackAtAsync(projectId, new DateTime(2021, 3, 10, 12, 0, 0, DateTimeKind.Utc), useful: true);

        var weeks = await _sut.GetStatsAsync(
            new DateTime(2021, 3, 8, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2021, 3, 14, 23, 59, 59, DateTimeKind.Utc),
            projectId);

        weeks.Count.ShouldBe(1);
        var project = weeks.Single().Projects.Single(p => p.ProjectId == projectId);
        project.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatsAsync_ProjectIdFilterGiven_RestrictsToSingleProject()
    {
        var filteredProjectId = await InsertProjectAsync();
        var otherProjectId = await InsertProjectAsync();
        await InsertFeedbackAtAsync(filteredProjectId, new DateTime(2022, 5, 4, 12, 0, 0, DateTimeKind.Utc), useful: true);
        await InsertFeedbackAtAsync(otherProjectId, new DateTime(2022, 5, 4, 12, 0, 0, DateTimeKind.Utc), useful: false);

        var weeks = await _sut.GetStatsAsync(
            new DateTime(2022, 5, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2022, 5, 8, 23, 59, 59, DateTimeKind.Utc),
            filteredProjectId);

        var week = weeks.Single();
        week.Projects.ShouldHaveSingleItem();
        week.Projects[0].ProjectId.ShouldBe(filteredProjectId);
        week.Projects[0].TotalCount.ShouldBe(1);
        week.Projects[0].UsefulCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatsAsync_NoProjectIdFilterGiven_IncludesEveryRegisteredProject()
    {
        var projectWithFeedback = await InsertProjectAsync();
        var projectWithoutFeedback = await InsertProjectAsync();
        await InsertFeedbackAtAsync(projectWithFeedback, new DateTime(2023, 9, 4, 12, 0, 0, DateTimeKind.Utc), useful: true);

        var weeks = await _sut.GetStatsAsync(
            new DateTime(2023, 9, 4, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2023, 9, 10, 23, 59, 59, DateTimeKind.Utc),
            null);

        var week = weeks.Single();
        week.Projects.ShouldContain(p => p.ProjectId == projectWithFeedback && p.TotalCount == 1);
        week.Projects.ShouldContain(p => p.ProjectId == projectWithoutFeedback && p.TotalCount == 0);
    }

    [Fact]
    public async Task ExportAsync_WithinWindow_ReturnsRowsOrderedByCreatedAtAscending()
    {
        var projectId = await InsertProjectAsync();

        var similarities = new[] { 0.91, 0.73 };
        await InsertFeedbackAtAsync(projectId, new DateTime(2024, 4, 10, 12, 0, 0, DateTimeKind.Utc), useful: false, similarities: similarities, reason: "not related");
        await InsertFeedbackAtAsync(projectId, new DateTime(2024, 4, 5, 12, 0, 0, DateTimeKind.Utc), useful: true, similarities: [], reason: null);

        var rows = await _sut.ExportAsync(
            new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 4, 30, 23, 59, 59, DateTimeKind.Utc),
            projectId);

        rows.Count.ShouldBe(2);
        rows[0].CreatedAt.ShouldBe(new DateTime(2024, 4, 5, 12, 0, 0, DateTimeKind.Utc));
        rows[0].Useful.ShouldBeTrue();
        rows[0].Similarities.ShouldBeEmpty();
        rows[0].Reason.ShouldBeNull();
        rows[1].CreatedAt.ShouldBe(new DateTime(2024, 4, 10, 12, 0, 0, DateTimeKind.Utc));
        rows[1].Useful.ShouldBeFalse();
        rows[1].Similarities.ShouldBe(similarities);
        rows[1].Reason.ShouldBe("not related");
        rows.ShouldAllBe(row => row.ProjectId == projectId && row.ProjectName != null);
    }

    [Fact]
    public async Task ExportAsync_CreatedAtOutsideWindow_IsExcluded()
    {
        var projectId = await InsertProjectAsync();
        await InsertFeedbackAtAsync(projectId, new DateTime(2021, 7, 1, 12, 0, 0, DateTimeKind.Utc), useful: true, similarities: [], reason: null);
        await InsertFeedbackAtAsync(projectId, new DateTime(2021, 7, 15, 12, 0, 0, DateTimeKind.Utc), useful: true, similarities: [], reason: null);

        var rows = await _sut.ExportAsync(
            new DateTime(2021, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2021, 7, 20, 23, 59, 59, DateTimeKind.Utc),
            projectId);

        rows.ShouldHaveSingleItem();
        rows[0].CreatedAt.ShouldBe(new DateTime(2021, 7, 15, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ExportAsync_ProjectIdFilterGiven_RestrictsToSingleProject()
    {
        var filteredProjectId = await InsertProjectAsync();
        var otherProjectId = await InsertProjectAsync();
        await InsertFeedbackAtAsync(filteredProjectId, new DateTime(2022, 8, 4, 12, 0, 0, DateTimeKind.Utc), useful: true, similarities: [], reason: null);
        await InsertFeedbackAtAsync(otherProjectId, new DateTime(2022, 8, 4, 12, 0, 0, DateTimeKind.Utc), useful: false, similarities: [], reason: null);

        var rows = await _sut.ExportAsync(
            new DateTime(2022, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2022, 8, 31, 23, 59, 59, DateTimeKind.Utc),
            filteredProjectId);

        rows.ShouldHaveSingleItem();
        rows[0].ProjectId.ShouldBe(filteredProjectId);
    }

    [Fact]
    public async Task ExportAsync_NoProjectIdFilterGiven_IncludesEveryRegisteredProject()
    {
        var firstProjectId = await InsertProjectAsync();
        var secondProjectId = await InsertProjectAsync();
        await InsertFeedbackAtAsync(firstProjectId, new DateTime(2023, 10, 4, 12, 0, 0, DateTimeKind.Utc), useful: true, similarities: [], reason: null);
        await InsertFeedbackAtAsync(secondProjectId, new DateTime(2023, 10, 4, 13, 0, 0, DateTimeKind.Utc), useful: false, similarities: [], reason: null);

        var rows = await _sut.ExportAsync(
            new DateTime(2023, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2023, 10, 31, 23, 59, 59, DateTimeKind.Utc),
            null);

        rows.ShouldContain(row => row.ProjectId == firstProjectId);
        rows.ShouldContain(row => row.ProjectId == secondProjectId);
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

    private async Task InsertFeedbackAtAsync(long projectId, DateTime createdAtUtc, bool useful, double[]? similarities = null, string? reason = null)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.code_query_feedback (project_id, question, useful, similarities, reason, username, created_at)
            VALUES (@ProjectId, 'stats test', @Useful, @Similarities, @Reason, 'tester', @CreatedAt)
            """,
            new { ProjectId = projectId, Useful = useful, Similarities = similarities ?? [], Reason = reason, CreatedAt = createdAtUtc });
    }
}
