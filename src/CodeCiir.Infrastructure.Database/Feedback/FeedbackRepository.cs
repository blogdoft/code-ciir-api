using CodeCiir.Application.Feedback;
using Dapper;
using Npgsql;

namespace CodeCiir.Infrastructure.Database.Feedback;

public sealed class FeedbackRepository(NpgsqlDataSource dataSource) : IFeedbackRepository
{
    public async Task<FeedbackResult> InsertAsync(
        long projectId,
        string question,
        bool useful,
        IReadOnlyList<double> similarities,
        string? reason,
        string user,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public.code_query_feedback (project_id, question, useful, similarities, reason, username)
            VALUES (@ProjectId, @Question, @Useful, @Similarities, @Reason, @User)
            RETURNING id AS Id, project_id AS ProjectId, question AS Question, useful AS Useful,
                      similarities AS Similarities, reason AS Reason, username AS User, created_at AS CreatedAt
            """;

        var parameters = new
        {
            ProjectId = projectId,
            Question = question,
            Useful = useful,
            Similarities = similarities.ToArray(),
            Reason = reason,
            User = user,
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleAsync<FeedbackRow>(command);
        return row.ToResult();
    }

    /// <summary>
    /// A mutable row type, not a record: Dapper's constructor-matching fast path for records
    /// requires each constructor parameter's type to exactly match the field type reported by
    /// the data reader - for a float8[]/double[] column, Npgsql reports the generic array type
    /// there, not the specific double array type, which breaks that match even though the
    /// actual runtime value is a real double array. Property-based mapping has no such
    /// requirement.
    /// </summary>
#pragma warning disable S3459, S1144
    private sealed class FeedbackRow
    {
        public long Id { get; set; }

        public long ProjectId { get; set; }

        public string Question { get; set; } = string.Empty;

        public bool Useful { get; set; }

        public double[] Similarities { get; set; } = [];

        public string? Reason { get; set; }

        public string User { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public FeedbackResult ToResult() => new(
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
}
