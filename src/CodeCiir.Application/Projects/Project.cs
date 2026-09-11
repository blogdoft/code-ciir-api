namespace CodeCiir.Application.Projects;

/// <summary>
/// A project stored in code3rag, normally created/managed by code-ciir-indexer but also
/// writable through this API's own CRUD endpoints - see .specs/03-projects-endpoint.md for the
/// tradeoffs of two independent writers on the same table.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record Project(
    long Id,
    string Name,
    string EmbeddingModel,
    int EmbeddingDimensions,
    Uri? GitUrl,
    Uri? GitRawUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt);
#pragma warning restore SA1313
