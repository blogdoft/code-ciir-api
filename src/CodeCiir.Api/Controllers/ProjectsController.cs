using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Api.Contracts;
using CodeCiir.Api.Problems;
using CodeCiir.Application.Projects;
using Microsoft.AspNetCore.Mvc;

namespace CodeCiir.Api.Controllers;

/// <summary>
/// Read-only lookup of projects indexed into code3rag by code-ciir-indexer. Unlike
/// code-rag-api, there is no create/rename/delete here - code-ciir-api does not own this
/// table (see .specs/03-projects-endpoint.md).
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "Projects")]
[Route("api/v1/projects")]
public sealed class ProjectsController(IProjectsService projectsService) : ControllerBase
{
    private const string GetProjectRouteName = "GetProject";

    /// <summary>List all projects</summary>
    /// <remarks>
    /// Returns all projects indexed into code3rag. Optionally filter the results by project name
    /// using a partial, case-insensitive match against the name field. Use the returned id
    /// values as the projectId path parameter when fetching a specific project, or when querying
    /// code via the /projects/{projectId}/code-queries endpoint.
    ///
    /// When no project matches the supplied filter (or no projects exist at all), the response is
    /// a 200 OK with an empty array - this is not treated as an error.
    /// </remarks>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">
    /// A list of projects matching the given filter, or all projects if no filter was supplied.
    /// Returns an empty array when there are no matches.
    /// </response>
    /// <response code="400">
    /// The name query parameter is invalid (e.g. it exceeds the maximum allowed length, or is
    /// present but empty). This is the only condition under which this endpoint returns 400.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<ProjectResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        // Read the raw query value instead of a bound [FromQuery] parameter: MVC's default
        // model binding treats an empty string as null (ConvertEmptyStringToNull), which would
        // make "?name=" indistinguishable from omitting the parameter entirely - the OpenAPI
        // contract requires the former to be a 400.
#pragma warning disable S6932
        var name = Request.Query.TryGetValue("name", out var values) ? values.ToString() : null;
#pragma warning restore S6932

        var result = await projectsService.ListAsync(name, cancellationToken);

        return result.Map(
            onSuccess: projects => (IActionResult)Ok(projects.Select(ToResponse)),
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
        if (!RouteId.TryParsePositive(projectId, "projectId", HttpContext.Request.Path, out var id, out var problem))
        {
            return problem!;
        }

        var result = await projectsService.GetAsync(id, cancellationToken);

        return result.Map(
            onSuccess: project => (IActionResult)Ok(ToResponse(project)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    private static ProjectResponse ToResponse(Project project) => new(
        project.Id,
        project.Name,
        project.EmbeddingModel,
        project.EmbeddingDimensions,
        project.CreatedAt,
        project.UpdatedAt);
}
