using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Projects;

public sealed class ProjectsService(IProjectsRepository projectsRepository) : IProjectsService
{
    public const int MaxNameFilterLength = 200;
    public const int MaxNameLength = 200;
    public const int MaxEmbeddingModelLength = 200;
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

    public async Task<Result<Project>> GetAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var project = await projectsRepository.GetByIdAsync(projectId, cancellationToken);
        return project is null
            ? ProjectFailures.ProjectNotFound(projectId)
            : project;
    }

    public async Task<Result<Project>> CreateAsync(
        string? name,
        string? embeddingModel,
        int? embeddingDimensions,
        Uri? gitUrl,
        Uri? gitRawUrl,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateFields(name, embeddingModel, embeddingDimensions);
        if (validation is not null)
        {
            return validation;
        }

        if (await projectsRepository.ExistsByNameAsync(name!, excludingId: null, cancellationToken))
        {
            return ProjectFailures.NameConflict(name!);
        }

        return await projectsRepository.InsertAsync(
            name!,
            embeddingModel!,
            embeddingDimensions!.Value,
            gitUrl,
            gitRawUrl,
            cancellationToken);
    }

    public async Task<Result<Project>> UpdateAsync(
        long projectId,
        string? name,
        string? embeddingModel,
        int? embeddingDimensions,
        Uri? gitUrl,
        Uri? gitRawUrl,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateFields(name, embeddingModel, embeddingDimensions);
        if (validation is not null)
        {
            return validation;
        }

        var existing = await projectsRepository.GetByIdAsync(projectId, cancellationToken);
        if (existing is null)
        {
            return ProjectFailures.ProjectNotFound(projectId);
        }

        if (await projectsRepository.ExistsByNameAsync(name!, excludingId: projectId, cancellationToken))
        {
            return ProjectFailures.NameConflict(name!);
        }

        var updated = await projectsRepository.UpdateAsync(
            projectId,
            name!,
            embeddingModel!,
            embeddingDimensions!.Value,
            gitUrl,
            gitRawUrl,
            cancellationToken);
        return updated is null
            ? ProjectFailures.ProjectNotFound(projectId)
            : updated;
    }

    public async Task<Result<bool>> DeleteAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var deleted = await projectsRepository.DeleteAsync(projectId, cancellationToken);
        return deleted
            ? Result<bool>.FromSuccess(true)
            : ProjectFailures.ProjectNotFound(projectId);
    }

    private static Failure? ValidateFields(string? name, string? embeddingModel, int? embeddingDimensions)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ProjectFailures.NameRequired();
        }

        if (name.Length > MaxNameLength)
        {
            return ProjectFailures.NameTooLong(MaxNameLength);
        }

        if (string.IsNullOrWhiteSpace(embeddingModel))
        {
            return ProjectFailures.EmbeddingModelRequired();
        }

        if (embeddingModel.Length > MaxEmbeddingModelLength)
        {
            return ProjectFailures.EmbeddingModelTooLong(MaxEmbeddingModelLength);
        }

        if (embeddingDimensions is null)
        {
            return ProjectFailures.EmbeddingDimensionsRequired();
        }

        if (embeddingDimensions <= 0)
        {
            return ProjectFailures.EmbeddingDimensionsInvalid();
        }

        return null;
    }
}
