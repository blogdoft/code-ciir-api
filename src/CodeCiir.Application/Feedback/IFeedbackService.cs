using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Feedback;

public interface IFeedbackService
{
    Task<Result<FeedbackResult>> SubmitAsync(
        long projectId,
        string? question,
        bool? useful,
        IReadOnlyList<double>? similarities,
        string? reason,
        string? user,
        CancellationToken cancellationToken = default);
}
