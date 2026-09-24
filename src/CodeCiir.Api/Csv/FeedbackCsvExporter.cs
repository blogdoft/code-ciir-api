using CodeCiir.Application.Feedback;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.Text.Json;

namespace CodeCiir.Api.Csv;

/// <summary>
/// Renders feedback export rows as the documented CSV contract (a wire format of its own: column
/// order and snake_case header names are fixed, independent of the JSON camelCase convention).
/// </summary>
internal static class FeedbackCsvExporter
{
    public static byte[] ToCsvBytes(IReadOnlyList<FeedbackExportRow> rows, TimeZoneInfo? timezone)
    {
        using var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream, leaveOpen: true))
        using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
        {
            csvWriter.WriteRecords(rows.Select(row => ToCsvRecord(row, timezone)));
        }

        return memoryStream.ToArray();
    }

    public static string ToFileName(FeedbackExportResult export, long? projectId)
    {
        var projectSuffix = projectId is null ? string.Empty : $"_project-{projectId}";
        return $"feedback_export_{export.StartDate:yyyyMMdd}_{export.EndDate:yyyyMMdd}{projectSuffix}.csv";
    }

    private static FeedbackExportCsvRecord ToCsvRecord(FeedbackExportRow row, TimeZoneInfo? timezone) => new(
        row.Id,
        row.ProjectId,
        row.ProjectName,
        row.Question,
        row.Useful,
        JsonSerializer.Serialize(row.Similarities),
        row.Reason,
        row.Username,
        FormatCreatedAt(row.CreatedAt, timezone));

    // Default (no timezone given): "Z"-suffixed UTC, matching the API's JSON contract elsewhere.
    // With a timezone: converted to that zone's local wall-clock, with an explicit numeric offset
    // ("zzz") instead of "Z" - the offset can differ per row's date for zones with DST.
    private static string FormatCreatedAt(DateTime createdAtUtc, TimeZoneInfo? timezone)
    {
        var utcOffset = new DateTimeOffset(createdAtUtc, TimeSpan.Zero);
        return timezone is null
            ? utcOffset.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            : TimeZoneInfo.ConvertTime(utcOffset, timezone).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties. Column order and snake_case names match the documented CSV contract.
    // created_at is a pre-formatted string (not a typed DateTime with a static CsvHelper
    // [Format]) because its format depends on the optional timezone query parameter at request
    // time, not on a fixed attribute.
#pragma warning disable SA1313
    private sealed record FeedbackExportCsvRecord(
        [property: Name("id")] long Id,
        [property: Name("project_id")] long ProjectId,
        [property: Name("project_name")] string ProjectName,
        [property: Name("question")] string Question,
        [property: Name("useful")] bool Useful,
        [property: Name("similarities")] string Similarities,
        [property: Name("reason")] string? Reason,
        [property: Name("username")] string Username,
        [property: Name("created_at")] string CreatedAt);
#pragma warning restore SA1313
}
