using CodeCiir.Application.Projects;
using CodeCiir.Application.Tests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.Projects.ProjectsServiceTests;

public sealed class GetAsyncTests : BaseProjectsServiceTests
{
    [Fact]
    public async Task Should_ReturnTheProject_When_ItExists()
    {
        var project = ProjectFaker.Create();
        ProjectsRepository.GetByPublicIdAsync(project.PublicId, Arg.Any<CancellationToken>()).Returns(project);

        var result = await Sut.GetAsync(project.PublicId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(project);
    }

    [Fact]
    public async Task Should_ReturnProjectNotFoundFailure_When_ProjectDoesNotExist()
    {
        var missingId = Guid.NewGuid();
        ProjectsRepository.GetByPublicIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await Sut.GetAsync(missingId);

        result.ShouldBeFailure(ProjectFailures.ProjectNotFound(missingId));
    }
}
