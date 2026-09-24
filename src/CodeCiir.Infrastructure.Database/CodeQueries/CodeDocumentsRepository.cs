using BlogDoFT.Libs.DapperUtils.Abstractions.Extensions;
using BlogDoFT.Libs.DapperUtils.Postgres;
using CodeCiir.Application.CodeQueries;
using Dapper;
using Npgsql;
using Pgvector;

namespace CodeCiir.Infrastructure.Database.CodeQueries;

public sealed class CodeDocumentsRepository(NpgsqlDataSource dataSource) : ICodeDocumentsRepository
{
    public async Task<CodeDocumentSource?> GetSourceAsync(long documentId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT cd.id AS DocumentId
                 , cd.source_path AS SourceFile
                 , CASE
                       WHEN NULLIF(BTRIM(p.git_raw_url), '') IS NULL OR cd.source_path IS NULL THEN NULL
                       ELSE RTRIM(p.git_raw_url, '/') || '/' || LTRIM(cd.source_path, '/')
                   END AS GitRawUrl
            FROM public.ciir_documents cd
            INNER JOIN public.projects p ON p.id = cd.project_id
            WHERE cd.id = @DocumentId
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { DocumentId = documentId }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<CodeDocumentSourceProjection>(command);

        return row?.ToSource();
    }

    public async Task<IEnumerable<CodeQueryResult>> SearchAsync(
        IReadOnlyList<float> queryEmbedding,
        double? minSimilarity,
        long? projectId,
        string? kind,
        QualifiedNameFilterOperator? qualifiedNameOperator,
        string? qualifiedNameValue,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var where = new WhereBuilder();
        where.AndWith(projectId, "cd.project_id = @ProjectId");
        where.AndWith(kind, "cd.kind = @KindValue");
        if (qualifiedNameOperator is not null && qualifiedNameValue is not null)
        {
            where.AndWith(qualifiedNameValue, QualifiedNameCondition(qualifiedNameOperator.Value));
        }

        var newFilters = where.Build().ToString();

        // Build() returns "" when none of the filters above were added, or "where (cond1)  and
        // (cond2) ...". The "where " keyword is dropped and what remains is spliced in front of
        // the fixed conditions below, so the optional filters always come first in the WHERE clause.
        var newFiltersPrefix = newFilters.Length > 0
            ? newFilters["where ".Length..] + " and "
            : string.Empty;

        // The interpolated fragment is limited to fixed, developer-controlled column names and
        // SQL keywords chosen by QualifiedNameCondition below - all caller-supplied values still
        // flow through Dapper parameters, so this isn't injectable.
#pragma warning disable S2077
        var sql = $"""
            SELECT cd.id AS Id
                 , cd.kind AS Kind
                 , cd.symbol_container AS SymbolContainer
                 , cd.symbol_name AS SymbolName
                 , cd.symbol_qualified_name AS SymbolQualifiedName
                 , cd.symbol_canonical_name AS SymbolCanonicalName
                 , cd.source_path AS SourceFile
                 , p.git_url AS GitUrl
                 , CASE
                       WHEN NULLIF(BTRIM(p.git_raw_url), '') IS NULL THEN NULL
                       ELSE RTRIM(p.git_raw_url, '/') || '/' || LTRIM(cd.source_path, '/')
                   END AS GitRawUrl
                 , cd.embedding_text AS EmbeddingText
                 , ROUND((1 - (cd.embedding <=> @Embedding))::numeric, 10)::float8 AS Similarity
            FROM public.ciir_documents cd
            INNER JOIN public.projects p ON p.id = cd.project_id
            WHERE {newFiltersPrefix}cd.embedding IS NOT NULL
              AND (1 - (cd.embedding <=> @Embedding)) >= @MinSimilarity
            ORDER BY cd.embedding <=> @Embedding
            LIMIT @Limit
            """;

        var parameters = new
        {
            ProjectId = projectId,
            Embedding = new Vector(queryEmbedding.ToArray()),
            Limit = limit,

            // Sentinel: no real cosine similarity is below double.MinValue, so omitting
            // minSimilarity makes this comparison a no-op instead of needing an "IS NULL OR" in SQL.
            MinSimilarity = minSimilarity ?? double.MinValue,

            KindValue = kind,

            // AsSqlWildCard (BlogDoFT.Libs.DapperUtils.Abstractions) turns the caller's '*' into
            // SQL's '%' for Contains/NotContains, so ILIKE @QualifiedNameValue is an exact
            // (case-insensitive) match unless the caller opts into a pattern. Equals passes the
            // raw value through unchanged for '='.
            QualifiedNameValue = qualifiedNameOperator is QualifiedNameFilterOperator.Contains or QualifiedNameFilterOperator.NotContains
                ? qualifiedNameValue?.AsSqlWildCard(toUpperCase: false)
                : qualifiedNameValue,
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
#pragma warning restore S2077
        var rows = await connection.QueryAsync<CodeQueryResultProjection>(command);

        return rows.Select(r => r.ToResult());
    }

    private static string QualifiedNameCondition(QualifiedNameFilterOperator qualifiedNameOperator) => qualifiedNameOperator switch
    {
        QualifiedNameFilterOperator.Equals => "cd.symbol_qualified_name = @QualifiedNameValue",
        QualifiedNameFilterOperator.Contains => "cd.symbol_qualified_name ILIKE @QualifiedNameValue",
        QualifiedNameFilterOperator.NotContains => "(cd.symbol_qualified_name IS NULL OR cd.symbol_qualified_name NOT ILIKE @QualifiedNameValue)",
        _ => throw new ArgumentOutOfRangeException(nameof(qualifiedNameOperator), qualifiedNameOperator, null),
    };

    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties - the standard .NET convention is PascalCase, matching the "AS Id",
    // "AS Kind", ... aliases in the SQL above that Dapper binds them from.
#pragma warning disable SA1313
    private sealed record CodeQueryResultProjection(
        long Id,
        string Kind,
        string? SymbolContainer,
        string? SymbolName,
        string? SymbolQualifiedName,
        string? SymbolCanonicalName,
        string? SourceFile,
        string? GitUrl,
        string? GitRawUrl,
        string? EmbeddingText,
        double Similarity)
    {
        public CodeQueryResult ToResult() => new(
            Id,
            Kind,
            SymbolContainer,
            SymbolName,
            SymbolQualifiedName,
            SymbolCanonicalName,
            SourceFile,
            EmbeddingText,
            Similarity,
            GitUrl: UriColumn.ToUriOrNull(GitUrl),
            GitRawUrl: UriColumn.ToUriOrNull(GitRawUrl));
    }
#pragma warning restore SA1313

    private sealed record CodeDocumentSourceProjection(long DocumentId, string? SourceFile, string? GitRawUrl)
    {
        public CodeDocumentSource ToSource() => new(
            DocumentId,
            SourceFile,
            UriColumn.ToUriOrNull(GitRawUrl));
    }
}
