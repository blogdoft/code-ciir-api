using BlogDoFT.Libs.DapperUtils.Abstractions;
using BlogDoFT.Libs.DapperUtils.Postgres;
using CodeCiir.Application.Projects;
using Dapper;
using Npgsql;

namespace CodeCiir.Infrastructure.Database.Projects;

public sealed class ProjectsRepository(NpgsqlDataSource dataSource) : IProjectsRepository
{
    private const string ResultSet = """
        SELECT id AS Id
             , name AS Name
             , embedding_model AS EmbeddingModel
             , embedding_dimensions AS EmbeddingDimensions
             , git_url AS GitUrl
             , git_raw_url AS GitRawUrl
             , created_at AS CreatedAt
             , updated_at AS UpdatedAt
        FROM public.projects
        """;

    public async Task<(IReadOnlyList<Project> Items, long TotalCount)> SearchAsync(
        string? nameFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // The interpolated fragments below are limited to a fixed, developer-controlled shape that
        // WhereBuilder/PaginatedSqlBuilder either includes verbatim or omits entirely - the
        // caller-supplied value still flows through the @NameFilter Dapper parameter, so this
        // isn't injectable.
#pragma warning disable S2077
        var (query, querySize) = new PaginatedSqlBuilder()
            .WithResultSet(ResultSet)
            .WithWhere(where => where.AndWith(nameFilter, "name ILIKE '%' || @NameFilter || '%'"))
            .MappingOrderWith("name", "name")
            .WithPagination(new PageFilter { Page = page, Size = pageSize, Order = "name" })
            .Build();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var itemsCommand = new CommandDefinition(query.ToString(), new { NameFilter = nameFilter }, cancellationToken: cancellationToken);
        var countCommand = new CommandDefinition(querySize.ToString(), new { NameFilter = nameFilter }, cancellationToken: cancellationToken);
#pragma warning restore S2077

        var rows = await connection.QueryAsync<ProjectRow>(itemsCommand);
        var totalCount = await connection.ExecuteScalarAsync<long>(countCommand);

        return (rows.Select(r => r.ToProject()).ToList(), totalCount);
    }

    public async Task<Project?> GetByIdAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            {ResultSet}
            WHERE id = @ProjectId
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProjectRow>(command);
        return row?.ToProject();
    }

    public async Task<bool> ExistsByNameAsync(string name, long? excludingId, CancellationToken cancellationToken = default)
    {
#pragma warning disable S2077
        var where = new WhereBuilder()
            .AndWith(name, "name = @Name")
            .AndWith(excludingId, "id <> @ExcludingId")
            .Build();

        var sql = $"SELECT EXISTS (SELECT 1 FROM public.projects {where})";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Name = name, ExcludingId = excludingId }, cancellationToken: cancellationToken);
#pragma warning restore S2077
        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task<Project> InsertAsync(
        string name,
        string embeddingModel,
        int embeddingDimensions,
        Uri? gitUrl,
        Uri? gitRawUrl,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public.projects (name, embedding_model, embedding_dimensions, git_url, git_raw_url)
            VALUES (@Name, @EmbeddingModel, @EmbeddingDimensions, @GitUrl, @GitRawUrl)
            RETURNING id AS Id
                    , name AS Name
                    , embedding_model AS EmbeddingModel
                    , embedding_dimensions AS EmbeddingDimensions
                    , git_url AS GitUrl
                    , git_raw_url AS GitRawUrl
                    , created_at AS CreatedAt
                    , updated_at AS UpdatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Name = name,
                EmbeddingModel = embeddingModel,
                EmbeddingDimensions = embeddingDimensions,
                GitUrl = gitUrl?.ToString(),
                GitRawUrl = gitRawUrl?.ToString(),
            },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleAsync<ProjectRow>(command);
        return row.ToProject();
    }

    public async Task<Project?> UpdateAsync(
        long projectId,
        string name,
        string embeddingModel,
        int embeddingDimensions,
        Uri? gitUrl,
        Uri? gitRawUrl,
        CancellationToken cancellationToken = default)
    {
        const string SQL = """
            UPDATE public.projects
            SET name = @Name
              , embedding_model = @EmbeddingModel
              , embedding_dimensions = @EmbeddingDimensions
              , git_url = @GitUrl
              , git_raw_url = @GitRawUrl
              , updated_at = now()
            WHERE id = @ProjectId
            RETURNING id AS Id
                    , name AS Name
                    , embedding_model AS EmbeddingModel
                    , embedding_dimensions AS EmbeddingDimensions
                    , git_url AS GitUrl
                    , git_raw_url AS GitRawUrl
                    , created_at AS CreatedAt
                    , updated_at AS UpdatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SQL,
            new
            {
                ProjectId = projectId,
                Name = name,
                GitUrl = gitUrl?.ToString(),
                GitRawUrl = gitRawUrl?.ToString(),
                EmbeddingModel = embeddingModel,
                EmbeddingDimensions = embeddingDimensions,
            },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProjectRow>(command);
        return row?.ToProject();
    }

    public async Task<bool> DeleteAsync(long projectId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM public.projects WHERE id = @ProjectId";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken);
        var affected = await connection.ExecuteAsync(command);
        return affected > 0;
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
        string? GitUrl,
        string? GitRawUrl,
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
            GitUrl is null ? null : new Uri(GitUrl),
            GitRawUrl is null ? null : new Uri(GitRawUrl),
            DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc),
            DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc));
    }
#pragma warning restore SA1313
}
