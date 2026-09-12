using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.CodeQueries;

public sealed class CodeDocumentSourceService(ICodeDocumentsRepository codeDocumentsRepository) : ICodeDocumentSourceService
{
    public async Task<Result<CodeDocumentSource>> GetAsync(long documentId, CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            return CodeQueryFailures.CodeDocumentIdInvalid();
        }

        var source = await codeDocumentsRepository.GetSourceAsync(documentId, cancellationToken);
        return source is null
            ? CodeQueryFailures.CodeDocumentNotFound(documentId)
            : Result<CodeDocumentSource>.FromSuccess(source);
    }
}
