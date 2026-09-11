using BlogDoFT.Libs.DapperUtils.Postgres;
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

    public async Task<IReadOnlyList<WeeklyFeedbackStats>> GetStatsAsync(
        DateTime startDate,
        DateTime endDate,
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        // Dense week x project grid: "weeks" enumerates every ISO calendar week (Monday - Postgres'
        // date_trunc('week', ...) truncates to Monday) overlapping the window, "eligible_projects"
        // is either every registered project or the single one matching projectId, and the CROSS
        // JOIN + LEFT JOIN guarantees every (week, project) pair appears at least once, zero-filled
        // when there's no matching feedback.
        var eligibleProjectsWhere = new WhereBuilder().AndWith(projectId, "id = @ProjectId").Build();

        // The interpolated fragment is limited to a fixed, developer-controlled condition
        // (id = @ProjectId) that WhereBuilder either includes verbatim or omits entirely - the
        // caller-supplied value itself still flows through the @ProjectId Dapper parameter below,
        // so this isn't injectable.
#pragma warning disable S2077
        var sql = $"""
            WITH weeks AS (
                SELECT generate_series(
                    date_trunc('week', @StartDate::timestamptz),
                    date_trunc('week', @EndDate::timestamptz),
                    interval '7 days'
                ) AS week_start
            ),
            eligible_projects AS (
                SELECT id, name FROM public.projects
                {eligibleProjectsWhere}
            )
            SELECT
                w.week_start AS WeekStart,
                p.id AS ProjectId,
                p.name AS ProjectName,
                COUNT(f.id) AS TotalCount,
                COUNT(f.id) FILTER (WHERE f.useful) AS UsefulCount,
                COUNT(f.id) FILTER (WHERE NOT f.useful) AS NotUsefulCount
            FROM weeks w
            CROSS JOIN eligible_projects p
            LEFT JOIN public.code_query_feedback f
                ON f.project_id = p.id
                AND f.created_at >= @StartDate AND f.created_at <= @EndDate
                AND date_trunc('week', f.created_at) = w.week_start
            GROUP BY w.week_start, p.id, p.name
            ORDER BY w.week_start, p.id
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { StartDate = startDate, EndDate = endDate, ProjectId = projectId },
            cancellationToken: cancellationToken);
#pragma warning restore S2077
        var rows = await connection.QueryAsync<StatsRow>(command);

        // GroupBy preserves source order (per SQL's ORDER BY) both across groups and within each
        // group, so no explicit re-sort is needed here.
        return rows
            .GroupBy(row => row.WeekStart)
            .Select(group =>
            {
                // DateOnly.FromDateTime only copies the Y/M/D components - the DateTimeKind
                // Npgsql assigns to a timestamptz column (Unspecified) doesn't matter here,
                // unlike full-datetime serialization elsewhere in this repo (see CreatedAt below).
                var weekStart = DateOnly.FromDateTime(group.Key);
                return new WeeklyFeedbackStats(weekStart, weekStart.AddDays(6), group.Select(row => row.ToProjectFeedbackStats()).ToList());
            })
            .ToList();
    }

    public async Task<IReadOnlyList<FeedbackExportRow>> ExportAsync(
        DateTime startDate,
        DateTime endDate,
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        var where = new WhereBuilder()
            .AndWith(startDate, "f.created_at >= @StartDate")
            .AndWith(endDate, "f.created_at <= @EndDate")
            .AndWith(projectId, "f.project_id = @ProjectId")
            .Build();

        // The interpolated fragment is limited to fixed, developer-controlled conditions that
        // WhereBuilder either includes verbatim or omits entirely - the caller-supplied values
        // still flow through Dapper parameters below, so this isn't injectable.
#pragma warning disable S2077
        var sql = $"""
            SELECT
                f.id AS Id,
                f.project_id AS ProjectId,
                p.name AS ProjectName,
                f.question AS Question,
                f.useful AS Useful,
                f.similarities AS Similarities,
                f.reason AS Reason,
                f.username AS Username,
                f.created_at AS CreatedAt
            FROM public.code_query_feedback f
            JOIN public.projects p ON p.id = f.project_id
            {where}
            ORDER BY f.created_at ASC
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { StartDate = startDate, EndDate = endDate, ProjectId = projectId },
            cancellationToken: cancellationToken);
#pragma warning restore S2077
        var rows = await connection.QueryAsync<ExportRow>(command);
        return rows.Select(row => row.ToFeedbackExportRow()).ToList();
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

    // Same double[] mapping quirk as FeedbackRow above - property-setter POCO, not a positional record.
#pragma warning disable S3459, S1144
    private sealed class ExportRow
    {
        public long Id { get; set; }

        public long ProjectId { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        public string Question { get; set; } = string.Empty;

        public bool Useful { get; set; }

        public double[] Similarities { get; set; } = [];

        public string? Reason { get; set; }

        public string Username { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public FeedbackExportRow ToFeedbackExportRow() => new(
            Id, ProjectId, ProjectName, Question, Useful, Similarities, Reason, Username, DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc));
    }
#pragma warning restore S3459, S1144

    // Positional record is safe here (unlike FeedbackRow/ExportRow above): every column is a
    // scalar type (timestamptz, int8, text, bigint) whose reader.GetFieldType(i) matches the
    // constructor parameter type exactly, so Dapper's constructor-matching fast path applies
    // without issue.
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
    private sealed record StatsRow(
        DateTime WeekStart,
        long ProjectId,
        string ProjectName,
        long TotalCount,
        long UsefulCount,
        long NotUsefulCount)
    {
        public ProjectFeedbackStats ToProjectFeedbackStats() => new(
            ProjectId,
            ProjectName,
            TotalCount,
            UsefulCount,
            NotUsefulCount,
            TotalCount == 0 ? 0 : Math.Round((double)UsefulCount / TotalCount * 100, 2),
            TotalCount == 0 ? 0 : Math.Round((double)NotUsefulCount / TotalCount * 100, 2));
    }
#pragma warning restore SA1313
}
