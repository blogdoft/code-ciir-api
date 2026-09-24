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
             , public_id AS PublicId
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

        var rows = await connection.QueryAsync<ProjectTable>(itemsCommand);
        var totalCount = await connection.ExecuteScalarAsync<long>(countCommand);

        return (rows.Select(r => r.ToDomain()).ToList(), totalCount);
    }

    public async Task<Project?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            {ResultSet}
            WHERE public_id = @PublicId
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { PublicId = publicId }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProjectTable>(command);
        return row?.ToDomain();
    }
}
