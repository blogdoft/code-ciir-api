using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Projects;

public interface IProjectsService
{
    Task<Result<ProjectPage>> ListAsync(
        string? nameFilter,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<Project>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}
