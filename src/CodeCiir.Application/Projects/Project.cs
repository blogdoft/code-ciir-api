namespace CodeCiir.Application.Projects;

/// <summary>
/// A project indexed by code-ciir-indexer into code3rag. Read-only from this API's point of
/// view - see .specs/03-projects-endpoint.md for why lifecycle management stays out of scope.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record Project(
    long Id,
    string Name,
    string EmbeddingModel,
    int EmbeddingDimensions,
    DateTime CreatedAt,
    DateTime UpdatedAt);
#pragma warning restore SA1313
