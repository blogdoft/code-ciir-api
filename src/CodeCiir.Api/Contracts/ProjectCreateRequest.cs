using System.Text.Json.Serialization;

namespace CodeCiir.Api.Contracts;

/// <summary>Fields required to create a new project.</summary>
/// <param name="Name">
/// The project's name. Always required, must not be empty or blank, and must not already be
/// used by another project (409 otherwise).
/// </param>
/// <param name="EmbeddingModel">
/// The embedding model this project's code documents are (or will be) embedded with. Always
/// required, must not be empty or blank.
/// </param>
/// <param name="EmbeddingDimensions">The dimensionality of that embedding model's vectors. Always required, must be a positive integer.</param>
/// <param name="GitUrl">The project's public Git URL.</param>
/// <param name="GitRawUrl">The project's public git raw url. The source will be concatenated to this to provide visualization feature.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectCreateRequest(
    string? Name,
    string? EmbeddingModel,
    int? EmbeddingDimensions,
    Uri? GitUrl,
    Uri? GitRawUrl);
#pragma warning restore SA1313
