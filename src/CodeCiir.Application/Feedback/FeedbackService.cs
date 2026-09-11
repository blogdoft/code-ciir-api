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

    /// <summary>Default window size (in days) applied by GetStatsAsync when start/end date are omitted or only one is given.</summary>
    public const int DefaultWindowDays = 30;

    /// <summary>Maximum allowed window size (in days, ~12 months) for GetStatsAsync/ExportAsync.</summary>
    public const int MaxWindowDays = 366;

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

    public async Task<Result<FeedbackStatsResult>> GetStatsAsync(
        DateTime? startDate,
        DateTime? endDate,
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        // Both given: start_date > end_date is only meaningful to reject when the caller
        // actually supplied both ends themselves - the default-window derivation below can never
        // produce an inverted range on its own.
        if (startDate is not null && endDate is not null && startDate > endDate)
        {
            return FeedbackFailures.InvalidDateRange();
        }

        var effectiveEnd = endDate ?? startDate?.AddDays(DefaultWindowDays) ?? DateTime.UtcNow;
        var effectiveStart = startDate ?? effectiveEnd.AddDays(-DefaultWindowDays);

        if (effectiveEnd - effectiveStart > TimeSpan.FromDays(MaxWindowDays))
        {
            return FeedbackFailures.WindowTooLarge();
        }

        if (projectId is not null)
        {
            var projectExists = await projectsRepository.GetByIdAsync(projectId.Value, cancellationToken) is not null;
            if (!projectExists)
            {
                return ProjectFailures.ProjectNotFound(projectId.Value);
            }
        }

        var weeks = await feedbackRepository.GetStatsAsync(effectiveStart, effectiveEnd, projectId, cancellationToken);

        return Result<FeedbackStatsResult>.FromSuccess(new FeedbackStatsResult(effectiveStart, effectiveEnd, weeks));
    }

    public async Task<Result<FeedbackExportResult>> ExportAsync(
        DateTime? startDate,
        DateTime? endDate,
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        // Unlike GetStatsAsync, each side defaults independently of the other - no ±N-days
        // derivation from whichever side was given.
        var effectiveStart = startDate ?? StartOfCurrentUtcMonth();
        var effectiveEnd = endDate ?? DateTime.UtcNow;

        if (effectiveStart > effectiveEnd)
        {
            return FeedbackFailures.InvalidDateRange();
        }

        if (effectiveEnd - effectiveStart > TimeSpan.FromDays(MaxWindowDays))
        {
            return FeedbackFailures.WindowTooLarge();
        }

        if (projectId is not null)
        {
            var projectExists = await projectsRepository.GetByIdAsync(projectId.Value, cancellationToken) is not null;
            if (!projectExists)
            {
                return ProjectFailures.ProjectNotFound(projectId.Value);
            }
        }

        var rows = await feedbackRepository.ExportAsync(effectiveStart, effectiveEnd, projectId, cancellationToken);

        return Result<FeedbackExportResult>.FromSuccess(new FeedbackExportResult(effectiveStart, effectiveEnd, rows));
    }

    private static DateTime StartOfCurrentUtcMonth()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
