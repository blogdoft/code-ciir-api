using System.Text.Json.Serialization;

namespace CodeCiir.Api.Contracts;

/// <summary>
/// Natural language search request, optionally narrowed by project and structured filters.
/// </summary>
/// <param name="Question">
/// A natural language ask describing the code the caller is looking for. This text is embedded
/// (using the app-wide configured embedding model, see EmbeddingOptions) and compared against
/// the stored code documents' embeddings using cosine similarity. Always required, must not be
/// empty or blank.
/// </param>
/// <param name="ProjectId">
/// Optional filter narrowing results to a single project, corresponding to the <c>id</c> field
/// returned by <c>GET /projects</c>. Omit to search across every project. When provided, must be
/// a positive 64-bit integer and must correspond to an existing project (404 otherwise).
/// </param>
/// <param name="MinSimilarity">Optional minimum cosine similarity (0.0-1.0) a match must have to be included.</param>
/// <param name="Kind">Optional filter narrowing results to code documents whose <c>kind</c> equals this value exactly. Omit for no filtering on this field.</param>
/// <param name="QualifiedName">Optional filter narrowing results by the member's fully qualified name. Omit for no filtering on this field.</param>
/// <param name="Limit">
/// Optional maximum number of matches to return (default 10, capped at 50). There is no
/// page-based pagination on this endpoint - see <c>CodeQueriesController.QueryAsync</c>'s remarks
/// for why.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryRequest(
    string? Question,
    long? ProjectId = null,
    double? MinSimilarity = null,
    string? Kind = null,
    CodeQueryQualifiedNameFilterRequest? QualifiedName = null,
    int? Limit = null);
#pragma warning restore SA1313
