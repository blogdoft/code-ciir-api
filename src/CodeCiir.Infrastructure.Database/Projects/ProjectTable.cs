using CodeCiir.Application.Projects;

namespace CodeCiir.Infrastructure.Database.Projects;

/// <summary>
/// Row shape of <c>public.projects</c>. Read-only here: the table is owned and written by
/// code-ciir-indexer, so there is deliberately no <c>FromDomain</c>.
/// </summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention is PascalCase, matching the "AS Id", "AS Name", ...
// aliases in ProjectsRepository's SQL that Dapper binds them from.
#pragma warning disable SA1313
internal sealed record ProjectTable(
    long Id,
    string Name,
    string EmbeddingModel,
    int EmbeddingDimensions,
    string? GitUrl,
    string? GitRawUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    // projects.created_at/updated_at are stored as timestamptz (always UTC); Npgsql returns them
    // with Kind=Unspecified, so it must be stamped explicitly to serialize with a "Z" suffix.
    public Project ToDomain() => new(
        Id,
        Name,
        EmbeddingModel,
        EmbeddingDimensions,
        GitUrl is null ? null : new Uri(GitUrl),
        GitRawUrl is null ? null : new Uri(GitRawUrl),
        DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
        DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc));
}
#pragma warning restore SA1313
