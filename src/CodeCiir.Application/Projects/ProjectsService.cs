using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Projects;

public sealed class ProjectsService(IProjectsRepository projectsRepository) : IProjectsService
{
    public const int MaxNameFilterLength = 200;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public async Task<Result<ProjectPage>> ListAsync(
        string? nameFilter,
        int? page,
        int? pageSize,
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

        var resolvedPage = page ?? 0;
        if (resolvedPage < 0)
        {
            return ProjectFailures.PageInvalid();
        }

        var resolvedPageSize = pageSize ?? DefaultPageSize;
        if (resolvedPageSize < 1 || resolvedPageSize > MaxPageSize)
        {
            return ProjectFailures.PageSizeInvalid(MaxPageSize);
        }

        var (items, totalCount) = await projectsRepository.SearchAsync(nameFilter, resolvedPage, resolvedPageSize, cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)resolvedPageSize);

        return new ProjectPage(items, resolvedPage, resolvedPageSize, totalCount, totalPages);
    }

    public async Task<Result<Project>> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectsRepository.GetByPublicIdAsync(projectId, cancellationToken);
        return project is null
            ? ProjectFailures.ProjectNotFound(projectId)
            : project;
    }
}
