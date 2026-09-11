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
        projectsService.ListAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromSuccess(new ProjectPage(projects, 0, 20, 2, 1)));

        using var client = CreateClient(projectsService);
        using var response = await client.GetAsync(new Uri("/api/v1/projects", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProjectListDto>();
        body.ShouldNotBeNull();
        body.Items.Count.ShouldBe(2);
        body.Total_count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_EmptyNameQueryParam_ReturnsBadRequest()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService.ListAsync(string.Empty, null, null, Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromFailure(ProjectFailures.NameFilterEmpty()));

        using var client = CreateClient(projectsService);
        using var response = await client.GetAsync(new Uri("/api/v1/projects?name=", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task ListAsync_PageAndPageSizeQueryParams_ArePassedThrough()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService.ListAsync(null, 2, 5, Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromSuccess(new ProjectPage([], 2, 5, 0, 0)));

        using var client = CreateClient(projectsService);
        using var response = await client.GetAsync(new Uri("/api/v1/projects?page=2&page_size=5", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await projectsService.Received(1).ListAsync(null, 2, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_PageOutOfRange_ReturnsBadRequest()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService.ListAsync(null, -1, null, Arg.Any<CancellationToken>())
            .Returns(Result<ProjectPage>.FromFailure(ProjectFailures.PageInvalid()));

        using var client = CreateClient(projectsService);
        using var response = await client.GetAsync(new Uri("/api/v1/projects?page=-1", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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

    [Fact]
    public async Task CreateAsync_ValidBody_ReturnsCreatedWithLocation()
    {
        var created = CreateProject(7);
        var projectsService = Substitute.For<IProjectsService>();
        projectsService
            .CreateAsync(
                "proj-7",
                "bge-m3",
                1024,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<Project>.FromSuccess(created));

        using var client = CreateClient(projectsService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/projects", UriKind.Relative),
            new { name = "proj-7", embedding_model = "bge-m3", embedding_dimensions = 1024 });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.ToString().ShouldContain("/api/v1/projects/7");
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        body.ShouldNotBeNull();
        body.Id.ShouldBe(7);
    }

    [Fact]
    public async Task CreateAsync_NameConflict_ReturnsConflict()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService
            .CreateAsync(
                "dup",
                "bge-m3",
                1024,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<Project>.FromFailure(ProjectFailures.NameConflict("dup")));

        using var client = CreateClient(projectsService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/projects", UriKind.Relative),
            new { name = "dup", embedding_model = "bge-m3", embedding_dimensions = 1024 });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task CreateAsync_MissingName_ReturnsBadRequest()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService
            .CreateAsync(
                null,
                "bge-m3",
                1024,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<Project>.FromFailure(ProjectFailures.NameRequired()));

        using var client = CreateClient(projectsService);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/projects", UriKind.Relative),
            new { embedding_model = "bge-m3", embedding_dimensions = 1024 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAsync_ValidBody_ReturnsOkWithUpdatedProject()
    {
        var updated = CreateProject(7) with { Name = "renamed" };
        var projectsService = Substitute.For<IProjectsService>();
        projectsService
            .UpdateAsync(
                7,
                "renamed",
                "bge-m3",
                1024,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<Project>.FromSuccess(updated));

        using var client = CreateClient(projectsService);
        using var response = await client.PutAsJsonAsync(
            new Uri("/api/v1/projects/7", UriKind.Relative),
            new { name = "renamed", embedding_model = "bge-m3", embedding_dimensions = 1024 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("renamed");
    }

    [Fact]
    public async Task UpdateAsync_NonNumericProjectId_ReturnsBadRequestWithoutCallingService()
    {
        var projectsService = Substitute.For<IProjectsService>();

        using var client = CreateClient(projectsService);
        using var response = await client.PutAsJsonAsync(
            new Uri("/api/v1/projects/not-a-number", UriKind.Relative),
            new { name = "x", embedding_model = "bge-m3", embedding_dimensions = 1024 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await projectsService.DidNotReceive()
            .UpdateAsync(
                Arg.Any<long>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int?>(),
                Arg.Any<Uri?>(),
                Arg.Any<Uri?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_MissingProject_ReturnsNotFoundWithEmptyBody()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService
            .UpdateAsync(
                999,
                "proj",
                "bge-m3",
                1024,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<Project>.FromFailure(ProjectFailures.ProjectNotFound(999)));

        using var client = CreateClient(projectsService);
        using var response = await client.PutAsJsonAsync(
            new Uri("/api/v1/projects/999", UriKind.Relative),
            new { name = "proj", embedding_model = "bge-m3", embedding_dimensions = 1024 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_ExistingProject_ReturnsNoContent()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService.DeleteAsync(7, Arg.Any<CancellationToken>()).Returns(Result<bool>.FromSuccess(true));

        using var client = CreateClient(projectsService);
        using var response = await client.DeleteAsync(new Uri("/api/v1/projects/7", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteAsync_MissingProject_ReturnsNotFoundWithEmptyBody()
    {
        var projectsService = Substitute.For<IProjectsService>();
        projectsService.DeleteAsync(999, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.FromFailure(ProjectFailures.ProjectNotFound(999)));

        using var client = CreateClient(projectsService);
        using var response = await client.DeleteAsync(new Uri("/api/v1/projects/999", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_NonNumericProjectId_ReturnsBadRequestWithoutCallingService()
    {
        var projectsService = Substitute.For<IProjectsService>();

        using var client = CreateClient(projectsService);
        using var response = await client.DeleteAsync(new Uri("/api/v1/projects/not-a-number", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await projectsService.DidNotReceive().DeleteAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    private static Project CreateProject(long id) =>
        new(
            id,
            $"project-{id}",
            "bge-m3",
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"),
            DateTime.UtcNow,
            DateTime.UtcNow);

    private HttpClient CreateClient(IProjectsService projectsService) => factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProjectsService>();
            services.AddScoped(_ => projectsService);
        }))
        .CreateClient();

    private sealed record ProjectDto(long Id, string Name, string Embedding_model, int Embedding_dimensions);

    private sealed record ProjectListDto(List<ProjectDto> Items, int Page, int Page_size, long Total_count, int Total_pages);
}
