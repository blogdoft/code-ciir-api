namespace CodeCiir.Application.CodeQueries;

public interface ICodeDocumentsRepository
{
    /// <summary>
    /// Resolves the file reference for one indexed document. The returned source path is relative
    /// to the project root and the raw URL, when configured, identifies that exact file.
    /// </summary>
    Task<CodeDocumentSource?> GetSourceAsync(
        long documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the code documents whose embedding is closest (cosine similarity) to
    /// <paramref name="queryEmbedding"/>, ordered by descending similarity, narrowed by whichever
    /// optional filters are supplied and capped at <paramref name="limit"/> rows.
    /// </summary>
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
