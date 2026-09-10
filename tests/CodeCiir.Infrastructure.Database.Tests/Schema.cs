namespace CodeCiir.Infrastructure.Database.Tests;

/// <summary>
/// DDL mirroring code-ciir-indexer's InitialSchema migration
/// (sauron/code-ciir-indexer, src/Ciir.Indexer.Infrastructure.PostgreSql/Migrations/Migrations/
/// M20260908000000_InitialSchema.cs), confirmed against a live introspection of code3rag - see
/// .specs/01-schema-discovery.md. This API never runs migrations against the real database; this
/// DDL exists purely to seed a disposable Testcontainers instance for these tests.
/// </summary>
internal static class Schema
{
    public const string CreateExtensionVector = "CREATE EXTENSION IF NOT EXISTS vector;";

    public const string CreateProjects = """
        CREATE TABLE public.projects (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name text NOT NULL,
            embedding_model text NOT NULL,
            embedding_dimensions integer NOT NULL,
            created_at timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
            updated_at timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
            CONSTRAINT ux_projects_name UNIQUE (name)
        );
        """;

    public const string CreateCiirDocuments = """
        CREATE TABLE public.ciir_documents (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            project_id bigint NOT NULL REFERENCES public.projects(id),
            ciir_id text NOT NULL,
            schema_version text NOT NULL,
            kind text NOT NULL,
            language text NOT NULL,
            symbol_name text,
            symbol_qualified_name text,
            symbol_canonical_name text,
            symbol_container text,
            source_path text,
            embedding_text text,
            embedding_text_strategy text,
            embedding_text_hash text,
            embedding_model text,
            embedding_dimensions integer,
            embedding_fingerprint_hash text,
            content jsonb NOT NULL,
            last_seen_run_id uuid,
            created_at timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
            updated_at timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
            embedding vector,
            CONSTRAINT ux_ciir_documents_project_ciir_id UNIQUE (project_id, ciir_id)
        );
        CREATE INDEX ix_ciir_documents_project_id ON public.ciir_documents (project_id);
        CREATE INDEX ix_ciir_documents_kind ON public.ciir_documents (kind);
        CREATE INDEX ix_ciir_documents_symbol_qualified_name ON public.ciir_documents (symbol_qualified_name);
        """;

    public const string CreateCiirRelations = """
        CREATE TABLE public.ciir_relations (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            project_id bigint NOT NULL REFERENCES public.projects(id),
            source_ciir_id text NOT NULL,
            target_ciir_id text,
            source_document_id bigint REFERENCES public.ciir_documents(id) ON DELETE CASCADE,
            target_document_id bigint REFERENCES public.ciir_documents(id) ON DELETE CASCADE,
            kind text NOT NULL,
            target_symbol text NOT NULL,
            resolution_status text NOT NULL,
            resolution_origin text NOT NULL,
            resolution_reason text,
            source_path text,
            start_line integer,
            start_column integer,
            end_line integer,
            end_column integer,
            idempotency_key varchar(64) NOT NULL,
            last_seen_run_id uuid,
            created_at timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
            updated_at timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
            CONSTRAINT ux_ciir_relations_idempotency_key UNIQUE (idempotency_key)
        );
        CREATE INDEX ix_ciir_relations_project_id ON public.ciir_relations (project_id);
        CREATE INDEX ix_ciir_relations_source_document_kind ON public.ciir_relations (source_document_id, kind);
        CREATE INDEX ix_ciir_relations_target_document_kind ON public.ciir_relations (target_document_id, kind);
        """;

    // Not part of code-ciir-indexer's schema - this is the one table code-ciir-api owns
    // additively inside code3rag, per .specs/06-code-query-feedback.md.
    public const string CreateCodeQueryFeedback = """
        CREATE TABLE public.code_query_feedback (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            project_id bigint NOT NULL REFERENCES public.projects(id),
            question text NOT NULL,
            useful boolean NOT NULL,
            similarities float8[] NOT NULL,
            reason text,
            username text NOT NULL,
            created_at timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
        );
        """;

    public static string CreateTables => string.Join(
        '\n', CreateProjects, CreateCiirDocuments, CreateCiirRelations, CreateCodeQueryFeedback);

    public static string CreateAll => string.Join('\n', CreateExtensionVector, CreateTables);
}
