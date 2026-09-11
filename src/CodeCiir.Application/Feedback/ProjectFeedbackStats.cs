namespace CodeCiir.Application.Feedback;

/// <summary>Feedback effectiveness statistics for a single project within a single week.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectFeedbackStats(
    long ProjectId,
    string ProjectName,
    long TotalCount,
    long UsefulCount,
    long NotUsefulCount,
    double UsefulPercentage,
    double NotUsefulPercentage);
#pragma warning restore SA1313
