namespace CodeCiir.Api.Contracts;

/// <summary>
/// A project indexed into code3rag by code-ciir-indexer. Serializes as snake_case. Unlike
/// code-rag-api's ProjectResponse, there is no git_url/git_raw_url (code3rag's projects table
/// has no equivalent columns - see .specs/01-schema-discovery.md) and there is no corresponding
/// write endpoint in this API (see .specs/03-projects-endpoint.md for why).
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectResponse(
    long Id,
    string Name,
    string EmbeddingModel,
    int EmbeddingDimensions,
    DateTime CreatedAt,
    DateTime UpdatedAt);
#pragma warning restore SA1313
