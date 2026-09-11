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

    /// <summary>
    /// Returns whether a project named <paramref name="name"/> already exists, excluding the
    /// project identified by <paramref name="excludingId"/> (if given) from the check - used to
    /// allow an update to keep a project's own current name.
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, long? excludingId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new project and returns the persisted record, including its generated id and timestamps.</summary>
    Task<Project> InsertAsync(
        string name,
        string embeddingModel,
        int embeddingDimensions,
        Uri? gitUrl,
        Uri? gitRawUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces every field of the project identified by <paramref name="projectId"/> and returns
    /// the updated record, or null when no project exists with that id.
    /// </summary>
    Task<Project?> UpdateAsync(
        long projectId,
        string name,
        string embeddingModel,
        int embeddingDimensions,
        Uri? gitUrl,
        Uri? gitRawUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the project identified by <paramref name="projectId"/>. Returns whether a row was deleted.</summary>
    Task<bool> DeleteAsync(long projectId, CancellationToken cancellationToken = default);
}
