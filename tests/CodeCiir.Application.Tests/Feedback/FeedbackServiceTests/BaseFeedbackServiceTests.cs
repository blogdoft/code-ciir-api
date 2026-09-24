using Bogus;
using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using CodeCiir.Application.Tests.Support;
using NSubstitute;

namespace CodeCiir.Application.Tests.Feedback.FeedbackServiceTests;

public abstract class BaseFeedbackServiceTests
{
    protected const string Agent = "claude code";

    protected IProjectsRepository ProjectsRepository { get; } = Substitute.For<IProjectsRepository>();

    protected IFeedbackRepository FeedbackRepository { get; } = Substitute.For<IFeedbackRepository>();

    protected Faker Faker { get; } = new();

    protected FeedbackService Sut => new(ProjectsRepository, FeedbackRepository);

    protected static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    protected void GivenProjectExists(long projectId) => ProjectsRepository
        .GetByIdAsync(projectId, Arg.Any<CancellationToken>())
        .Returns(ProjectFaker.Create() with { Id = projectId });

    protected void GivenProjectDoesNotExist(long projectId) => ProjectsRepository
        .GetByIdAsync(projectId, Arg.Any<CancellationToken>())
        .Returns((Project?)null);
}
