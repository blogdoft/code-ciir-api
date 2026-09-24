using CodeCiir.Application.Feedback;

namespace CodeCiir.Infrastructure.Database.Feedback;

/// <summary>
/// Row shape of <c>public.code_query_feedback</c> - the one table code-ciir-api owns inside
/// code3rag (see .specs/06-code-query-feedback.md).
/// </summary>
// A property-setter POCO rather than a positional record: Dapper's constructor-matching fast path
// can't bind Postgres' float8[] column to a double[] constructor parameter, but does bind it to a
// settable property. The setters are only ever used reflectively by Dapper.
#pragma warning disable S3459, S1144
internal sealed class CodeQueryFeedbackTable
{
    public long Id { get; set; }

    public long ProjectId { get; set; }

    public string Question { get; set; } = string.Empty;

    public bool Useful { get; set; }

    public double[] Similarities { get; set; } = [];

    public string? Reason { get; set; }

    public string User { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Row for an INSERT: <c>Id</c> and <c>CreatedAt</c> are left to the database to generate.</summary>
    /// <param name="feedback">The validated feedback about to be recorded.</param>
    /// <returns>The table row carrying its values.</returns>
    public static CodeQueryFeedbackTable FromDomain(NewFeedback feedback) => new()
    {
        ProjectId = feedback.ProjectId,
        Question = feedback.Question,
        Useful = feedback.Useful,
        Similarities = [.. feedback.Similarities],
        Reason = feedback.Reason,
        User = feedback.User,
    };

    public FeedbackResult ToDomain() => new(
        Id,
        ProjectId,
        Question,
        Useful,
        Similarities,
        Reason,
        User,
        DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc));
}
#pragma warning restore S3459, S1144
