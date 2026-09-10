using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Projects;

public sealed class ProjectsService(IProjectsRepository projectsRepository) : IProjectsService
{
    public const int MaxNameFilterLength = 200;

    public async Task<Result<IEnumerable<Project>>> ListAsync(
        string? nameFilter,
        CancellationToken cancellationToken = default)
    {
        if (nameFilter is not null)
        {
            if (string.IsNullOrWhiteSpace(nameFilter))
            {
                return ProjectFailures.NameFilterEmpty();
            }

            if (nameFilter.Length > MaxNameFilterLength)
            {
                return ProjectFailures.NameFilterTooLong(MaxNameFilterLength);
            }
        }

        var projects = await projectsRepository.SearchAsync(nameFilter, cancellationToken);
        return Result<IEnumerable<Project>>.FromSuccess(projects);
    }

    public async Task<Result<Project>> GetAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectsRepository.GetByIdAsync(projectId, cancellationToken);
        return project is null
            ? ProjectFailures.ProjectNotFound(projectId)
            : project;
    }
}
