namespace CodeCiir.Application.Projects;

public interface IProjectsRepository
{
    /// <summary>
    /// Returns every project whose name matches <paramref name="nameFilter"/> (partial,
    /// case-insensitive), or every project when <paramref name="nameFilter"/> is null.
    /// </summary>
    Task<IEnumerable<Project>> SearchAsync(string? nameFilter, CancellationToken cancellationToken = default);

    /// <summary>Returns the project with the given id, or null when none exists.</summary>
    Task<Project?> GetByIdAsync(long projectId, CancellationToken cancellationToken = default);
}
