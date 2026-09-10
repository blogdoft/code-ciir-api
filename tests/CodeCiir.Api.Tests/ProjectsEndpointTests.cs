using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Projects;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CodeCiir.Api.Tests;

/// <summary>
/// HTTP end-to-end tests for /api/v1/projects. IProjectsService is substituted (NSubstitute) so
/// these exercise routing/serialization/Problem-Details mapping without needing a real database -
/// ProjectsRepository itself is covered against a real Postgres in
/// CodeCiir.Infrastructure.Database.Tests.
/// </summary>
public sealed class ProjectsEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task ListAsync_NoFilter_ReturnsOkWithProjects()
    {
        var projects = new[] { CreateProject(1), CreateProject(2) };
        var projectsService = Substitute.For<IProjectsService>();
        projectsService.ListAsync(null, Arg.Any<CancellationToken>())
            .Returns(Result<IEnumerable<Project>>.FromSuccess(projects));

        using var client = CreateClient(projectsService);
        using var response = await client.GetAsync(new Uri("/api/v1/projects", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ProjectDto>>();
        body.ShouldNotBeNull();
        body.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_EmptyNameQueryParam_ReturnsBadRequest()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService.ListAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(Result<IEnumerable<Project>>.FromFailure(ProjectFailures.NameFilterEmpty()));

        using var client = CreateClient(projectsService);
        using var response = await client.GetAsync(new Uri("/api/v1/projects?name=", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetAsync_NonNumericProjectId_ReturnsBadRequestWithoutCallingService()
    {
        var projectsService = Substitute.For<IProjectsService>();

        using var client = CreateClient(projectsService);
        using var response = await client.GetAsync(new Uri("/api/v1/projects/not-a-number", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await projectsService.DidNotReceive().GetAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_ExistingProject_ReturnsOk()
    {
        var project = CreateProject(42);
        var projectsService = Substitute.For<IProjectsService>();
        projectsService.GetAsync(42, Arg.Any<CancellationToken>()).Returns(Result<Project>.FromSuccess(project));

        using var client = CreateClient(projectsService);
        using var response = await client.GetAsync(new Uri("/api/v1/projects/42", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        body.ShouldNotBeNull();
        body.Id.ShouldBe(42);
    }

    [Fact]
    public async Task GetAsync_MissingProject_ReturnsNotFoundWithEmptyBody()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService.GetAsync(999, Arg.Any<CancellationToken>())
            .Returns(Result<Project>.FromFailure(ProjectFailures.ProjectNotFound(999)));

        using var client = CreateClient(projectsService);
        using var response = await client.GetAsync(new Uri("/api/v1/projects/999", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    private static Project CreateProject(long id) =>
        new(id, $"project-{id}", "bge-m3", 1024, DateTime.UtcNow, DateTime.UtcNow);

    private HttpClient CreateClient(IProjectsService projectsService) => factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProjectsService>();
            services.AddScoped(_ => projectsService);
        }))
        .CreateClient();

    private sealed record ProjectDto(long Id, string Name, string Embedding_model, int Embedding_dimensions);
}
