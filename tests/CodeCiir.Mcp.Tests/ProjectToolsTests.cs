using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Projects;
using CodeCiir.Mcp.Tools;
using ModelContextProtocol;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Mcp.Tests;

public sealed class ProjectToolsTests
{
    private readonly IProjectsService _projectsService = Substitute.For<IProjectsService>();
    private readonly ProjectTools _sut;

    public ProjectToolsTests()
    {
        _sut = new ProjectTools(_projectsService);
    }

    [Fact]
    public async Task ListProjectsAsync_PassesNameFilterThrough()
    {
        var project = new Project(
            1,
            "proj",
            "bge-m3",
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"),
            DateTime.UtcNow,
            DateTime.UtcNow);
        _projectsService.ListAsync("proj", null, null, Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromSuccess(new ProjectPage([project], 0, 20, 1, 1)));

        var result = await _sut.ListProjectsAsync("proj");

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe(1);
        result[0].Name.ShouldBe("proj");
    }

    [Fact]
    public async Task ListProjectsAsync_PassesPageAndPageSizeThrough()
    {
        _projectsService.ListAsync(null, 2, 5, Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromSuccess(new ProjectPage([], 2, 5, 0, 0)));

        var result = await _sut.ListProjectsAsync(page: 2, pageSize: 5);

        result.ShouldBeEmpty();
        await _projectsService.Received(1).ListAsync(null, 2, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListProjectsAsync_FailureResult_ThrowsMcpException()
    {
        _projectsService.ListAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromFailure(ProjectFailures.NameFilterTooLong(200)));

        await Should.ThrowAsync<McpException>(() => _sut.ListProjectsAsync(new string('a', 201)));
    }
}
