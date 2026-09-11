using System.Text.Json.Serialization;

namespace CodeCiir.Api.Contracts;

/// <summary>
/// Full replacement fields for an existing project. All fields are required, mirroring
/// <see cref="ProjectCreateRequest"/> - this is a full PUT replace, not a partial patch.
/// </summary>
/// <param name="Name">The project's new name. Must not be empty or blank, and must not already be used by another project (409 otherwise).</param>
/// <param name="EmbeddingModel">The project's new embedding model. Must not be empty or blank.</param>
/// <param name="EmbeddingDimensions">The project's new embedding dimensionality. Must be a positive integer.</param>
/// <param name="GitUrl">The project's public Git URL.</param>
/// <param name="GitRawUrl">The project's public git raw url. The source will be concatenated to this to provide visualization feature.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectUpdateRequest(
    string? Name,
    string? EmbeddingModel,
    int? EmbeddingDimensions,
    string? GitUrl,
    string? GitRawUrl);
#pragma warning restore SA1313
