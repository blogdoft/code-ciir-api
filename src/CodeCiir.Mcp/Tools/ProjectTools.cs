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
        "Lists projects indexed into code3rag by code-ciir-indexer, optionally filtered by a " +
        "partial, case-insensitive name match. Use the returned id as projectId for " +
        "query_project_code.")]
    public async Task<IReadOnlyList<ProjectToolResult>> ListProjectsAsync(
        [Description("Optional partial, case-insensitive filter on the project name.")] string? name = null,
        CancellationToken cancellationToken = default)
    {
        var result = await projectsService.ListAsync(name, cancellationToken);

        return result.Map(
            onSuccess: projects => (IReadOnlyList<ProjectToolResult>)projects.Select(ToResult).ToList(),
            onFailure: failure => throw new McpException(failure.Message));
    }

    private static ProjectToolResult ToResult(Project project) => new(
        project.Id, project.Name, project.EmbeddingModel, project.EmbeddingDimensions, project.CreatedAt, project.UpdatedAt);
}
