namespace CodeCiir.Application.CodeQueries;

public interface ICodeDocumentsRepository
{
    /// <summary>
    /// Resolves the file reference for one indexed document. The returned source path is relative
    /// to the project root and the raw URL, when configured, identifies that exact file.
    /// </summary>
    /// <param name="documentId">The internal id of the document to resolve.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
    Task<CodeDocumentSource?> GetSourceAsync(
        long documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the code documents whose embedding is closest (cosine similarity) to
    /// <paramref name="queryEmbedding"/>, ordered by descending similarity, narrowed by whichever
    /// optional filters are supplied and capped at <paramref name="limit"/> rows.
    /// </summary>
    /// <param name="queryEmbedding">The question's embedding to compare stored document embeddings against.</param>
    /// <param name="minSimilarity">Minimum cosine similarity a document must have to be included, or null for no floor.</param>
    /// <param name="projectId">Restricts the search to this project's documents, or null to search every project.</param>
    /// <param name="kind">Restricts the search to documents of this kind, or null for every kind.</param>
    /// <param name="qualifiedNameOperator">
    /// How <paramref name="qualifiedNameValue"/> is matched against a document's qualified name, or
    /// null when <paramref name="qualifiedNameValue"/> itself is null.
    /// </param>
    /// <param name="qualifiedNameValue">The qualified-name value to filter by, or null for no filter.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
    Task<IEnumerable<CodeQueryResult>> SearchAsync(
        IReadOnlyList<float> queryEmbedding,
        double? minSimilarity,
        long? projectId,
        string? kind,
        QualifiedNameFilterOperator? qualifiedNameOperator,
        string? qualifiedNameValue,
        int limit,
        CancellationToken cancellationToken = default);
}
