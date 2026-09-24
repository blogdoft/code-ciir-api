using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Projects;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CodeCiir.Mcp.Tools;

[McpServerToolType]
public sealed class ProjectTools(IProjectsService projectsService)
{
    [McpServerTool(Name = "list_projects")]
    [Description(
        "Lists projects stored in code3rag, optionally filtered by a partial, case-insensitive " +
        "name match. Use the returned id as projectId for query_project_code.")]
    public async Task<IReadOnlyList<ProjectToolResult>> ListProjectsAsync(
        [Description("Optional partial, case-insensitive filter on the project name.")] string? name = null,
        [Description("Zero-based page number to retrieve. Defaults to 0.")] int? page = null,
        [Description("Maximum number of projects per page. Defaults to 20, capped at 100.")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var result = await projectsService.ListAsync(name, page, pageSize, cancellationToken);

        return result.Map(
            onSuccess: projectPage => (IReadOnlyList<ProjectToolResult>)projectPage.Items.Select(ToResult).ToList(),
            onFailure: failure => throw new McpException(failure.Message));
    }

    private static ProjectToolResult ToResult(Project project) => new(
        project.PublicId, project.Name, project.EmbeddingModel, project.EmbeddingDimensions, project.CreatedAt, project.UpdatedAt);
}
