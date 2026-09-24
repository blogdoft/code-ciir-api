using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Feedback;
using ModelContextProtocol;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Mcp.Tests.CodeQueryToolsTests;

public sealed class SubmitCodeQueryFeedbackAsyncTests : BaseCodeQueryToolsTests
{
    private const string Agent = "claude code";

    [Fact]
    public async Task Should_PassParametersThroughAndMapTheStoredFeedback_When_ServiceSucceeds()
    {
        var question = Faker.Lorem.Sentence();
        var feedback = new FeedbackResult(1, 7, question, true, [0.9], null, Agent, DateTime.UtcNow);
        FeedbackService.SubmitAsync(7, question, true, Arg.Any<IReadOnlyList<double>>(), null, Agent, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromSuccess(feedback));

        var result = await Sut.SubmitCodeQueryFeedbackAsync(7, question, true, [0.9], Agent);

        result.Id.ShouldBe(feedback.Id);
        result.User.ShouldBe(Agent);
    }

    [Fact]
    public async Task Should_ThrowMcpExceptionWithTheFailureMessage_When_ServiceFails()
    {
        var failure = FeedbackFailures.UserRequired();
        FeedbackService.SubmitAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<double>>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackResult>.FromFailure(failure));

        var exception = await Should.ThrowAsync<McpException>(() => Sut.SubmitCodeQueryFeedbackAsync(1, Faker.Lorem.Sentence(), true, [], string.Empty));

        exception.Message.ShouldContain(failure.Message);
    }
}
