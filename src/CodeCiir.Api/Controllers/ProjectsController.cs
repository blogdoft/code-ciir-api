using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Api.Contracts;
using CodeCiir.Api.Problems;
using CodeCiir.Application.Projects;
using Microsoft.AspNetCore.Mvc;

namespace CodeCiir.Api.Controllers;

/// <summary>
/// Read-only access to projects stored in code3rag. code-ciir-indexer owns this table and is the
/// only writer - see .specs/03-projects-endpoint.md for why the create/update/delete endpoints
/// this controller used to expose were removed in favor of code-ciir-indexer's own CRUD API.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "Projects")]
[Route("api/v1/projects")]
public sealed class ProjectsController(IProjectsService projectsService) : ControllerBase
{
    private const string GetProjectRouteName = "GetProject";

    /// <summary>Search projects (paginated)</summary>
    /// <remarks>
    /// Returns a page of projects stored in code3rag. Optionally filter the results by project
    /// name using a partial, case-insensitive match against the name field. Use the returned id
    /// values as the projectId path parameter when fetching/updating/deleting a specific project,
    /// or when querying code via the /projects/{projectId}/code-queries endpoint.
    ///
    /// When no project matches the supplied filter (or no projects exist at all), the response is
    /// a 200 OK with an empty items array - this is not treated as an error.
    /// </remarks>
    /// <param name="page">Zero-based page number to retrieve. Defaults to 0. Must not be negative.</param>
    /// <param name="pageSize">Maximum number of projects per page. Defaults to 20, capped at 100. Must be a positive integer.</param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">The page of projects matching the given filter, or all projects if no filter was supplied.</response>
    /// <response code="400">
    /// The name query parameter is invalid (e.g. it exceeds the maximum allowed length, or is
    /// present but empty), or page/page_size is out of range. This is the only condition under
    /// which this endpoint returns 400.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
    [HttpGet]
    [ProducesResponseType<ProjectListResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> ListAsync(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        CancellationToken cancellationToken)
    {
        // Read the raw query value instead of a bound [FromQuery] parameter: MVC's default
        // model binding treats an empty string as null (ConvertEmptyStringToNull), which would
        // make "?name=" indistinguishable from omitting the parameter entirely - the OpenAPI
        // contract requires the former to be a 400.
#pragma warning disable S6932
        var name = Request.Query.TryGetValue("name", out var values) ? values.ToString() : null;
#pragma warning restore S6932

        var result = await projectsService.ListAsync(name, page, pageSize, cancellationToken);

        return result.Map(
            onSuccess: projectPage => (IActionResult)Ok(ToListResponse(projectPage)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    /// <summary>Get a project by id</summary>
    /// <remarks>Returns a single project by its id.</remarks>
    /// <param name="projectId">
    /// Identifier of the project, corresponding to the id field returned by GET /projects. Must
    /// be a positive 64-bit integer; any other format (e.g. a GUID or non-numeric string) results
    /// in a 400 response.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">The project matching projectId.</response>
    /// <response code="400">
    /// The projectId path parameter is not a valid positive integer. This is the only condition
    /// under which this endpoint returns 400.
    /// </response>
    /// <response code="404">
    /// No project exists with the given projectId. This is the only condition under which this
    /// endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
    [HttpGet("{projectId}", Name = GetProjectRouteName)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> GetAsync(string projectId, CancellationToken cancellationToken)
    {
        if (!RouteId.TryParsePositive(projectId, nameof(projectId), HttpContext.Request.Path, out var id, out var problem))
        {
            return problem!;
        }

        var result = await projectsService.GetAsync(id, cancellationToken);

        return result.Map(
            onSuccess: project => (IActionResult)Ok(ToResponse(project)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    private static ProjectListResponse ToListResponse(ProjectPage page) => new(
        page.Items.Select(ToResponse).ToList(),
        page.Page,
        page.PageSize,
        page.TotalCount,
        page.TotalPages);

    private static ProjectResponse ToResponse(Project project) => new(
        project.Id,
        project.Name,
        project.EmbeddingModel,
        project.EmbeddingDimensions,
        project.GitUrl,
        project.GitRawUrl,
        project.CreatedAt,
        project.UpdatedAt);
}
