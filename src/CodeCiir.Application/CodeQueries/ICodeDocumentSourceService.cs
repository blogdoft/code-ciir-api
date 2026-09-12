using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.CodeQueries;

public interface ICodeDocumentSourceService
{
    Task<Result<CodeDocumentSource>> GetAsync(long documentId, CancellationToken cancellationToken = default);
}
