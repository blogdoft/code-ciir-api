using CodeCiir.Application.Feedback;

namespace CodeCiir.Api.Contracts;

/// <summary>Feedback effectiveness statistics for a single project within a single week.</summary>
/// <param name="ProjectId">Id of the project these statistics belong to.</param>
/// <param name="ProjectName">Name of the project these statistics belong to.</param>
/// <param name="TotalCount">Total number of feedback records for this project within this week.</param>
/// <param name="UsefulCount">Number of feedback records with <c>useful = true</c> within this week.</param>
/// <param name="NotUsefulCount">Number of feedback records with <c>useful = false</c> within this week.</param>
/// <param name="UsefulPercentage">
/// <paramref name="UsefulCount"/> as a percentage of <paramref name="TotalCount"/> for this week,
/// rounded to 2 decimal places. 0 when <paramref name="TotalCount"/> is 0.
/// </param>
/// <param name="NotUsefulPercentage">
/// <paramref name="NotUsefulCount"/> as a percentage of <paramref name="TotalCount"/> for this
/// week, rounded to 2 decimal places. 0 when <paramref name="TotalCount"/> is 0.
/// </param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectFeedbackStatsResponse(
    long ProjectId,
    string ProjectName,
    long TotalCount,
    long UsefulCount,
    long NotUsefulCount,
    double UsefulPercentage,
    double NotUsefulPercentage)
{
    public static ProjectFeedbackStatsResponse From(ProjectFeedbackStats project) => new(
        project.ProjectId,
        project.ProjectName,
        project.TotalCount,
        project.UsefulCount,
        project.NotUsefulCount,
        project.UsefulPercentage,
        project.NotUsefulPercentage);
}
#pragma warning restore SA1313
