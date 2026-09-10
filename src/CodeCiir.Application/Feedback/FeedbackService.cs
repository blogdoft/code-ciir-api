using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Projects;

namespace CodeCiir.Application.Feedback;

public sealed class FeedbackService(
    IProjectsRepository projectsRepository,
    IFeedbackRepository feedbackRepository) : IFeedbackService
{
    public const int MaxQuestionLength = 1000;
    public const int MaxUserLength = 200;
    public const int MaxReasonLength = 1000;
    public const int MaxSimilaritiesCount = 50;

    public async Task<Result<FeedbackResult>> SubmitAsync(
        long projectId,
        string? question,
        bool? useful,
        IReadOnlyList<double>? similarities,
        string? reason,
        string? user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return FeedbackFailures.QuestionRequired();
        }

        if (question.Length > MaxQuestionLength)
        {
            return FeedbackFailures.QuestionTooLong(MaxQuestionLength);
        }

        if (useful is null)
        {
            return FeedbackFailures.UsefulRequired();
        }

        if (similarities is null)
        {
            return FeedbackFailures.SimilaritiesRequired();
        }

        if (similarities.Count > MaxSimilaritiesCount)
        {
            return FeedbackFailures.TooManySimilarities(MaxSimilaritiesCount);
        }

        if (string.IsNullOrWhiteSpace(user))
        {
            return FeedbackFailures.UserRequired();
        }

        if (user.Length > MaxUserLength)
        {
            return FeedbackFailures.UserTooLong(MaxUserLength);
        }

        if (reason is not null && reason.Length > MaxReasonLength)
        {
            return FeedbackFailures.ReasonTooLong(MaxReasonLength);
        }

        var projectExists = await projectsRepository.GetByIdAsync(projectId, cancellationToken) is not null;
        if (!projectExists)
        {
            return ProjectFailures.ProjectNotFound(projectId);
        }

        var feedback = await feedbackRepository.InsertAsync(projectId, question, useful.Value, similarities, reason, user, cancellationToken);
        return Result<FeedbackResult>.FromSuccess(feedback);
    }
}
