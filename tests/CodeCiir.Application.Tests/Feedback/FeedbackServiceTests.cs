using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.Feedback;

public sealed class FeedbackServiceTests
{
    private static readonly Project TestProject = new(1, "proj", "bge-m3", 1024, DateTime.UtcNow, DateTime.UtcNow);

    private readonly IProjectsRepository _projectsRepository = Substitute.For<IProjectsRepository>();
    private readonly IFeedbackRepository _feedbackRepository = Substitute.For<IFeedbackRepository>();
    private readonly FeedbackService _sut;

    public FeedbackServiceTests()
    {
        _sut = new FeedbackService(_projectsRepository, _feedbackRepository);
        _projectsRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestProject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SubmitAsync_MissingQuestion_ReturnsQuestionRequired(string? question)
    {
        var result = await _sut.SubmitAsync(1, question, true, [], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-question-required");
    }

    [Fact]
    public async Task SubmitAsync_MissingUseful_ReturnsUsefulRequired()
    {
        var result = await _sut.SubmitAsync(1, "question", null, [], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-useful-required");
    }

    [Fact]
    public async Task SubmitAsync_MissingSimilarities_ReturnsSimilaritiesRequired()
    {
        var result = await _sut.SubmitAsync(1, "question", true, null, null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-similarities-required");
    }

    [Fact]
    public async Task SubmitAsync_EmptySimilarities_IsValid()
    {
        _feedbackRepository.InsertAsync(1, "question", true, Arg.Any<IReadOnlyList<double>>(), null, "claude code", Arg.Any<CancellationToken>())
            .Returns(new FeedbackResult(1, 1, "question", true, [], null, "claude code", DateTime.UtcNow));

        var result = await _sut.SubmitAsync(1, "question", true, [], null, "claude code");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SubmitAsync_TooManySimilarities_ReturnsTooManySimilarities()
    {
        var similarities = Enumerable.Repeat(0.5, FeedbackService.MaxSimilaritiesCount + 1).ToList();

        var result = await _sut.SubmitAsync(1, "question", true, similarities, null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-too-many-similarities");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SubmitAsync_MissingUser_ReturnsUserRequired(string? user)
    {
        var result = await _sut.SubmitAsync(1, "question", true, [], null, user);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-user-required");
    }

    [Fact]
    public async Task SubmitAsync_ReasonTooLong_ReturnsReasonTooLong()
    {
        var tooLong = new string('a', FeedbackService.MaxReasonLength + 1);

        var result = await _sut.SubmitAsync(1, "question", false, [0.3], tooLong, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-reason-too-long");
    }

    [Fact]
    public async Task SubmitAsync_MissingProject_ReturnsProjectNotFound()
    {
        _projectsRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.SubmitAsync(999, "question", true, [], null, "claude code");

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("404-project-not-found");
    }

    [Fact]
    public async Task SubmitAsync_Valid_InsertsAndReturnsResult()
    {
        var expected = new FeedbackResult(1, 1, "question", true, [0.8, 0.6], null, "claude code", DateTime.UtcNow);
        _feedbackRepository.InsertAsync(1, "question", true, Arg.Any<IReadOnlyList<double>>(), null, "claude code", Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.SubmitAsync(1, "question", true, [0.8, 0.6], null, "claude code");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }
}
