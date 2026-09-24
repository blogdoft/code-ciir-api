using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using CodeCiir.Application.Tests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.Feedback.FeedbackServiceTests;

public sealed class SubmitAsyncTests : BaseFeedbackServiceTests
{
    private const long ProjectId = 1;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Should_ReturnQuestionRequiredFailure_When_QuestionIsMissingOrBlank(string? question)
    {
        var result = await Sut.SubmitAsync(ProjectId, question, true, [], null, Agent);

        result.ShouldBeFailure(FeedbackFailures.QuestionRequired());
    }

    [Fact]
    public async Task Should_ReturnQuestionTooLongFailure_When_QuestionExceedsMaximumLength()
    {
        var tooLong = new string('a', FeedbackService.MaxQuestionLength + 1);

        var result = await Sut.SubmitAsync(ProjectId, tooLong, true, [], null, Agent);

        result.ShouldBeFailure(FeedbackFailures.QuestionTooLong(FeedbackService.MaxQuestionLength));
    }

    [Fact]
    public async Task Should_ReturnUsefulRequiredFailure_When_UsefulIsMissing()
    {
        var result = await Sut.SubmitAsync(ProjectId, Faker.Lorem.Sentence(), null, [], null, Agent);

        result.ShouldBeFailure(FeedbackFailures.UsefulRequired());
    }

    [Fact]
    public async Task Should_ReturnSimilaritiesRequiredFailure_When_SimilaritiesAreMissing()
    {
        var result = await Sut.SubmitAsync(ProjectId, Faker.Lorem.Sentence(), true, null, null, Agent);

        result.ShouldBeFailure(FeedbackFailures.SimilaritiesRequired());
    }

    [Fact]
    public async Task Should_ReturnTooManySimilaritiesFailure_When_SimilaritiesExceedMaximumCount()
    {
        var similarities = Enumerable.Repeat(0.5, FeedbackService.MaxSimilaritiesCount + 1).ToList();

        var result = await Sut.SubmitAsync(ProjectId, Faker.Lorem.Sentence(), true, similarities, null, Agent);

        result.ShouldBeFailure(FeedbackFailures.TooManySimilarities(FeedbackService.MaxSimilaritiesCount));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Should_ReturnUserRequiredFailure_When_UserIsMissing(string? user)
    {
        var result = await Sut.SubmitAsync(ProjectId, Faker.Lorem.Sentence(), true, [], null, user);

        result.ShouldBeFailure(FeedbackFailures.UserRequired());
    }

    [Fact]
    public async Task Should_ReturnUserTooLongFailure_When_UserExceedsMaximumLength()
    {
        var tooLong = new string('a', FeedbackService.MaxUserLength + 1);

        var result = await Sut.SubmitAsync(ProjectId, Faker.Lorem.Sentence(), true, [], null, tooLong);

        result.ShouldBeFailure(FeedbackFailures.UserTooLong(FeedbackService.MaxUserLength));
    }

    [Fact]
    public async Task Should_ReturnReasonTooLongFailure_When_ReasonExceedsMaximumLength()
    {
        var tooLong = new string('a', FeedbackService.MaxReasonLength + 1);

        var result = await Sut.SubmitAsync(ProjectId, Faker.Lorem.Sentence(), false, [0.3], tooLong, Agent);

        result.ShouldBeFailure(FeedbackFailures.ReasonTooLong(FeedbackService.MaxReasonLength));
    }

    [Fact]
    public async Task Should_ReturnProjectNotFoundFailure_When_ProjectDoesNotExist()
    {
        const long missingProjectId = 999;
        GivenProjectDoesNotExist(missingProjectId);

        var result = await Sut.SubmitAsync(missingProjectId, Faker.Lorem.Sentence(), true, [], null, Agent);

        result.ShouldBeFailure(ProjectFailures.ProjectNotFound(missingProjectId));
        await FeedbackRepository.DidNotReceive().InsertAsync(Arg.Any<NewFeedback>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_InsertAndReturnTheStoredFeedback_When_RequestIsValid()
    {
        GivenProjectExists(ProjectId);
        var question = Faker.Lorem.Sentence();
        double[] similarities = [0.8, 0.6];
        var expected = new FeedbackResult(Faker.Random.Long(1, 1000), ProjectId, question, true, similarities, null, Agent, DateTime.UtcNow);
        FeedbackRepository
            .InsertAsync(
                Arg.Is<NewFeedback>(f => f.ProjectId == ProjectId
                    && f.Question == question
                    && f.Useful
                    && f.Similarities.SequenceEqual(similarities)
                    && f.Reason == null
                    && f.User == Agent),
                Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await Sut.SubmitAsync(ProjectId, question, true, similarities, null, Agent);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task Should_AcceptTheFeedback_When_SimilaritiesAreAnEmptyArray()
    {
        GivenProjectExists(ProjectId);
        var question = Faker.Lorem.Sentence();
        var expected = new FeedbackResult(1, ProjectId, question, true, [], null, Agent, DateTime.UtcNow);
        FeedbackRepository.InsertAsync(Arg.Any<NewFeedback>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await Sut.SubmitAsync(ProjectId, question, true, [], null, Agent);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Similarities.ShouldBeEmpty();
    }
}
