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

    /// <summary>
    /// Returns the project with the given public id - the identifier every API/MCP caller actually
    /// supplies - or null when none exists.
    /// </summary>
    /// <param name="publicId">The project's public id to look up.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
    Task<Project?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);
}
