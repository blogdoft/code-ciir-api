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
}
