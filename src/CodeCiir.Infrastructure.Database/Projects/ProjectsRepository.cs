using BlogDoFT.Libs.DapperUtils.Postgres;
using CodeCiir.Application.Projects;
using Dapper;
using Npgsql;

namespace CodeCiir.Infrastructure.Database.Projects;

public sealed class ProjectsRepository(NpgsqlDataSource dataSource) : IProjectsRepository
{
    public async Task<IEnumerable<Project>> SearchAsync(
        string? nameFilter,
        CancellationToken cancellationToken = default)
    {
        var where = new WhereBuilder().AndWith(nameFilter, "name ILIKE '%' || @NameFilter || '%'").Build();

        // The interpolated fragment is limited to a fixed, developer-controlled condition that
        // WhereBuilder either includes verbatim or omits entirely - the caller-supplied value
        // still flows through the @NameFilter Dapper parameter below, so this isn't injectable.
#pragma warning disable S2077
        var sql = $"""
            SELECT id AS Id
                 , name AS Name
                 , embedding_model AS EmbeddingModel
                 , embedding_dimensions AS EmbeddingDimensions
                 , created_at AS CreatedAt
                 , updated_at AS UpdatedAt
            FROM public.projects
            {where}
            ORDER BY name
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { NameFilter = nameFilter }, cancellationToken: cancellationToken);
#pragma warning restore S2077
        var rows = await connection.QueryAsync<ProjectRow>(command);

        return rows.Select(r => r.ToProject());
    }

    public async Task<Project?> GetByIdAsync(long projectId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id
                 , name AS Name
                 , embedding_model AS EmbeddingModel
                 , embedding_dimensions AS EmbeddingDimensions
                 , created_at AS CreatedAt
                 , updated_at AS UpdatedAt
            FROM public.projects
            WHERE id = @ProjectId
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProjectRow>(command);
        return row?.ToProject();
    }

    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties - the standard .NET convention is PascalCase, matching the "AS Id",
    // "AS Name", ... aliases in the SQL above that Dapper binds them from.
#pragma warning disable SA1313
    private sealed record ProjectRow(
        long Id,
        string Name,
        string EmbeddingModel,
        int EmbeddingDimensions,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        // projects.created_at/updated_at are stored as timestamptz (always UTC); Npgsql returns
        // them with Kind=Unspecified, so it must be stamped explicitly to serialize with a "Z"
        // suffix.
        public Project ToProject() => new(
            Id,
            Name,
            EmbeddingModel,
            EmbeddingDimensions,
            DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc));
    }
#pragma warning restore SA1313
}
