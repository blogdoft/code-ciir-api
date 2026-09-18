namespace CodeCiir.Application.Projects;

public interface IProjectsRepository
{
    /// <summary>
    /// Returns a page of projects whose name matches <paramref name="nameFilter"/> (partial,
    /// case-insensitive), or every project when <paramref name="nameFilter"/> is null, ordered by
    /// name, alongside the total number of matching rows across every page.
    /// </summary>
    Task<(IReadOnlyList<Project> Items, long TotalCount)> SearchAsync(
        string? nameFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the project with the given id, or null when none exists.</summary>
    Task<Project?> GetByIdAsync(long projectId, CancellationToken cancellationToken = default);
}
