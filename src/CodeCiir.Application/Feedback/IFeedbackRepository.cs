namespace CodeCiir.Application.Feedback;

public interface IFeedbackRepository
{
    Task<FeedbackResult> InsertAsync(
        long projectId,
        string question,
        bool useful,
        IReadOnlyList<double> similarities,
        string? reason,
        string user,
        CancellationToken cancellationToken = default);
}
