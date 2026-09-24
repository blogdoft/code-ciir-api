using BlogDoFT.Libs.ResultPattern;
using Bogus;
using CodeCiir.Application.Projects;
using CodeCiir.Mcp.Tests.Support;
using CodeCiir.Mcp.Tools;
using ModelContextProtocol;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Mcp.Tests;

public sealed class ProjectToolsTests
{
    private readonly IProjectsService _projectsService = Substitute.For<IProjectsService>();
    private readonly Faker _faker = new();

    private ProjectTools Sut => new(_projectsService);

    [Fact]
    public async Task Should_PassNameFilterThroughAndMapProjects_When_ServiceSucceeds()
    {
        var project = ProjectFaker.Create();
        _projectsService.ListAsync(project.Name, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromSuccess(new ProjectPage([project], 0, 20, 1, 1)));

        var result = await Sut.ListProjectsAsync(project.Name);

        result.Select(p => (p.Id, p.Name)).ShouldBe([(project.Id, project.Name)]);
    }

    [Fact]
    public async Task Should_PassPageAndPageSizeThrough_When_BothAreGiven()
    {
        _projectsService.ListAsync(null, 2, 5, Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromSuccess(new ProjectPage([], 2, 5, 0, 0)));

        var result = await Sut.ListProjectsAsync(page: 2, pageSize: 5);

        result.ShouldBeEmpty();
        await _projectsService.Received(1).ListAsync(null, 2, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowMcpExceptionWithTheFailureMessage_When_ServiceFails()
    {
        var failure = ProjectFailures.NameFilterTooLong(ProjectsService.MaxNameFilterLength);
        _projectsService.ListAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromFailure(failure));

        var exception = await Should.ThrowAsync<McpException>(() => Sut.ListProjectsAsync(_faker.Random.String2(ProjectsService.MaxNameFilterLength + 1)));

        exception.Message.ShouldContain(failure.Message);
    }
}
