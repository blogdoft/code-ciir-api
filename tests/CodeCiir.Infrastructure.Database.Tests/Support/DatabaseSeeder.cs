using Bogus;
using Dapper;
using Npgsql;
using Pgvector;

namespace CodeCiir.Infrastructure.Database.Tests.Support;

/// <summary>Inserts rows straight into the disposable test database, bypassing the code under test.</summary>
internal sealed class DatabaseSeeder(NpgsqlDataSource dataSource)
{
    private readonly Faker _faker = new();

    public async Task<long> InsertProjectAsync(
        string? name = null,
        string embeddingModel = "test-model",
        int embeddingDimensions = 3,
        string? gitUrl = null,
        string? gitRawUrl = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO public.projects (name, embedding_model, embedding_dimensions, git_url, git_raw_url)
            VALUES (@Name, @EmbeddingModel, @EmbeddingDimensions, @GitUrl, @GitRawUrl)
            RETURNING id
            """,
            new
            {
                Name = name ?? $"proj-{Guid.NewGuid():N}",
                EmbeddingModel = embeddingModel,
                EmbeddingDimensions = embeddingDimensions,
                GitUrl = gitUrl,
                GitRawUrl = gitRawUrl,
            });
    }

    public async Task<long> InsertDocumentAsync(
        long projectId,
        string symbolName,
        float[]? embedding = null,
        string kind = "method",
        string? qualifiedName = null,
        string? sourcePath = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO public.ciir_documents
                (project_id, ciir_id, schema_version, kind, language, symbol_name, symbol_qualified_name, source_path, embedding_text, content, embedding)
            VALUES
                (@ProjectId, @CiirId, '1.0', @Kind, 'csharp', @SymbolName, @QualifiedName, @SourcePath, @SymbolName, '{}'::jsonb, @Embedding)
            RETURNING id
            """,
            new
            {
                ProjectId = projectId,
                CiirId = $"sha256:{Guid.NewGuid():N}",
                Kind = kind,
                SymbolName = symbolName,
                QualifiedName = qualifiedName,
                SourcePath = sourcePath,
                Embedding = embedding is null ? null : new Vector(embedding),
            });
    }

    public async Task InsertRelationAsync(
        long projectId,
        long sourceDocumentId,
        long? targetDocumentId,
        string kind,
        string targetSymbol = "target",
        string resolutionStatus = "resolved",
        string resolutionOrigin = "project")
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.ciir_relations
                (project_id, source_ciir_id, source_document_id, target_document_id, kind, target_symbol,
                 resolution_status, resolution_origin, idempotency_key)
            VALUES
                (@ProjectId, @SourceCiirId, @SourceDocumentId, @TargetDocumentId, @Kind, @TargetSymbol,
                 @ResolutionStatus, @ResolutionOrigin, @IdempotencyKey)
            """,
            new
            {
                ProjectId = projectId,
                SourceCiirId = $"sha256:{Guid.NewGuid():N}",
                SourceDocumentId = sourceDocumentId,
                TargetDocumentId = targetDocumentId,
                Kind = kind,
                TargetSymbol = targetSymbol,
                ResolutionStatus = resolutionStatus,
                ResolutionOrigin = resolutionOrigin,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
            });
    }

    public async Task InsertFeedbackAtAsync(
        long projectId,
        DateTime createdAtUtc,
        bool useful,
        double[]? similarities = null,
        string? reason = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.code_query_feedback (project_id, question, useful, similarities, reason, username, created_at)
            VALUES (@ProjectId, @Question, @Useful, @Similarities, @Reason, 'tester', @CreatedAt)
            """,
            new
            {
                ProjectId = projectId,
                Question = _faker.Lorem.Sentence(),
                Useful = useful,
                Similarities = similarities ?? [],
                Reason = reason,
                CreatedAt = createdAtUtc,
            });
    }
}
