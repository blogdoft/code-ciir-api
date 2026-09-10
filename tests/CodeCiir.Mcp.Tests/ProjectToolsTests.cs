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
        var project = new Project(1, "proj", "bge-m3", 1024, DateTime.UtcNow, DateTime.UtcNow);
        _projectsService.ListAsync("proj", Arg.Any<CancellationToken>())
            .Returns(Result<IEnumerable<Project>>.FromSuccess([project]));

        var result = await _sut.ListProjectsAsync("proj");

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe(1);
        result[0].Name.ShouldBe("proj");
    }

    [Fact]
    public async Task ListProjectsAsync_FailureResult_ThrowsMcpException()
    {
        _projectsService.ListAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IEnumerable<Project>>.FromFailure(ProjectFailures.NameFilterTooLong(200)));

        await Should.ThrowAsync<McpException>(() => _sut.ListProjectsAsync(new string('a', 201)));
    }
}
