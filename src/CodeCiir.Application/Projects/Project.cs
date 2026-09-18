namespace CodeCiir.Application.Projects;

/// <summary>
/// A project stored in code3rag, owned and managed by code-ciir-indexer - see
/// .specs/03-projects-endpoint.md for why this API's projects endpoint is read-only.
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
