using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Projects;

public interface IProjectsService
{
    Task<Result<ProjectPage>> ListAsync(
        string? nameFilter,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<Project>> GetAsync(long projectId, CancellationToken cancellationToken = default);

    Task<Result<Project>> CreateAsync(
        string? name,
        string? embeddingModel,
        int? embeddingDimensions,
        Uri? gitUrl,
        Uri? gitRawUrl,
        CancellationToken cancellationToken = default);

    Task<Result<Project>> UpdateAsync(
        long projectId,
        string? name,
        string? embeddingModel,
        int? embeddingDimensions,
        Uri? gitUrl,
        Uri? gitRawUrl,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(long projectId, CancellationToken cancellationToken = default);
}
