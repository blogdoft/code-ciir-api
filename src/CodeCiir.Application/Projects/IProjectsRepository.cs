namespace CodeCiir.Application.Projects;

public interface IProjectsRepository
{
    /// <summary>
    /// Returns a page of projects whose name matches <paramref name="nameFilter"/> (partial,
    /// case-insensitive), or every project when <paramref name="nameFilter"/> is null, ordered by
    /// name, alongside the total number of matching rows across every page.
    /// </summary>
    /// <param name="nameFilter">Partial, case-insensitive name filter, or null to match every project.</param>
    /// <param name="page">Zero-based page number to retrieve.</param>
    /// <param name="pageSize">Maximum number of projects per page.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
    Task<(IReadOnlyList<Project> Items, long TotalCount)> SearchAsync(
        string? nameFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the project with the given public id - the identifier every API/MCP caller actually
    /// supplies - or null when none exists.
    /// </summary>
    /// <param name="publicId">The project's public id to look up.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
    Task<Project?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);
}
