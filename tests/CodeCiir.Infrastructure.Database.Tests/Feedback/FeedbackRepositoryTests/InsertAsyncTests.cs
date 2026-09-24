using CodeCiir.Application.Feedback;
using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.Feedback.FeedbackRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class InsertAsyncTests(PostgresFixture fixture) : BaseFeedbackRepositoryTests(fixture)
{
    [Fact]
    public async Task Should_PersistAndReturnTheFeedback_When_FeedbackIsValid()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var question = Faker.Lorem.Sentence();
        var feedback = new NewFeedback(projectId, question, true, [0.83, 0.71], null, "claude code");

        var result = await Sut.InsertAsync(feedback);

        result.Id.ShouldBeGreaterThan(0);
        result.ProjectId.ShouldBe(projectId);
        result.Question.ShouldBe(question);
        result.Useful.ShouldBeTrue();
        result.Similarities.ShouldBe([0.83, 0.71]);
        result.Reason.ShouldBeNull();
        result.User.ShouldBe("claude code");
    }

    [Fact]
    public async Task Should_ReturnCreatedAtAsUtc_When_FeedbackIsInserted()
    {
        var projectId = await Seeder.InsertProjectAsync();

        var result = await Sut.InsertAsync(new NewFeedback(projectId, Faker.Lorem.Sentence(), true, [], null, "codex"));

        result.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Should_PersistEmptySimilaritiesAndReason_When_SimilaritiesAreEmpty()
    {
        var projectId = await Seeder.InsertProjectAsync();

        var result = await Sut.InsertAsync(new NewFeedback(projectId, Faker.Lorem.Sentence(), false, [], "not relevant", "codex"));

        result.Similarities.ShouldBeEmpty();
        result.Reason.ShouldBe("not relevant");
    }
}
