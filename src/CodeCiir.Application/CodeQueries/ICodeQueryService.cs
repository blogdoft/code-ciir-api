using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.CodeQueries;

public interface ICodeQueryService
{
    Task<Result<CodeQueryResponse>> QueryAsync(
        string? question,
        Guid? projectId = null,
        double? minSimilarity = null,
        string? kind = null,
        QualifiedNameFilterOperator? qualifiedNameOperator = null,
        string? qualifiedNameValue = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}
