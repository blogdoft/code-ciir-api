using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Projects;

public interface IProjectsService
{
    Task<Result<IEnumerable<Project>>> ListAsync(string? nameFilter, CancellationToken cancellationToken = default);

    Task<Result<Project>> GetAsync(long projectId, CancellationToken cancellationToken = default);
}
