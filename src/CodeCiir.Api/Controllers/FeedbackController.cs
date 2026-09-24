using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Api.Contracts;
using CodeCiir.Api.Csv;
using CodeCiir.Api.Problems;
using CodeCiir.Application.Feedback;
using Microsoft.AspNetCore.Mvc;

namespace CodeCiir.Api.Controllers;

/// <summary>Reporting operations over previously submitted code-query feedback.</summary>
[ApiController]
[ApiExplorerSettings(GroupName = "code-queries")]
[Route("api/code-queries/feedback")]
public sealed class FeedbackController(IFeedbackService feedbackService) : ControllerBase
{
    /// <summary>Get feedback effectiveness statistics, grouped by week and by project</summary>
    /// <remarks>
    /// Returns feedback effectiveness statistics for a time window (up to 12 months), broken down
    /// into a dense weekly x project grid: every ISO calendar week (Monday-Sunday) overlapping
    /// the window is included, and within every week, every registered project (or the single
    /// project matching projectId, if given) is included, even when it has zero feedback in that
    /// specific week. Each project entry within a week reports useful vs. not-useful counts and
    /// percentages for feedback created in that week.
    ///
    /// startDate and endDate are both optional and independent. When neither is given, the window
    /// defaults to the last 30 days ending now (UTC). When only one is given, the other end of the
    /// window is derived as a 30-day span from the given value (startDate + 30 days, or
    /// endDate - 30 days). When both are given, the exact window requested is used. startDate
    /// after endDate is a 400. The effective window (endDate - startDate) must not exceed 366
    /// days (12 months); exceeding it is also a 400.
    /// </remarks>
    /// <param name="startDate">
    /// Inclusive lower bound (UTC, ISO 8601) of the time window to aggregate feedback over.
    /// Optional - see the endpoint description for the default-window rules that apply when this
    /// and/or endDate are omitted, and for the 366-day maximum window size.
    /// </param>
    /// <param name="endDate">
    /// Inclusive upper bound (UTC, ISO 8601) of the time window to aggregate feedback over.
    /// Optional - see the endpoint description for the default-window rules that apply when this
    /// and/or startDate are omitted, and for the 366-day maximum window size.
    /// </param>
    /// <param name="projectId">
    /// Restrict every week's project list to a single project, instead of all registered
    /// projects. Must correspond to a project id returned by the list_projects MCP tool; a
    /// projectId that does not match any project results in a 404.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">
    /// Feedback statistics for the requested time window, as a dense grid of every week
    /// overlapping the window, each containing every registered project (or a single project when
    /// projectId was given), with zero-filled entries where there was no feedback. startDate/
    /// endDate in the response body reflect the effective window actually used, including when
    /// derived by default.
    /// </response>
    /// <response code="400">
    /// startDate or endDate is not a valid date-time, startDate is after endDate when both are
    /// given, or the effective window exceeds 366 days. This is the only client-error condition
    /// other than 404 under which this endpoint fails.
    /// </response>
    /// <response code="404">
    /// projectId was given but does not correspond to any registered project. This is the only
    /// condition under which this endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
    [HttpGet("stats")]
    [ProducesResponseType<CodeQueryFeedbackStatsResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStatsAsync(
        [FromQuery(Name = "startDate")] DateTimeOffset? startDate,
        [FromQuery(Name = "endDate")] DateTimeOffset? endDate,
        [FromQuery(Name = "projectId")] Guid? projectId,
        CancellationToken cancellationToken)
    {
        var result = await feedbackService.GetStatsAsync(
            startDate?.UtcDateTime,
            endDate?.UtcDateTime,
            projectId,
            cancellationToken);

        return result.Map(
            onSuccess: stats => (IActionResult)Ok(CodeQueryFeedbackStatsResponse.From(stats)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    /// <summary>Export raw feedback records for a time window (and optionally a single project) as CSV</summary>
    /// <remarks>
    /// Returns a downloadable CSV file with one row per feedback record in code_query_feedback
    /// whose created_at falls within [startDate, endDate], optionally restricted to a single
    /// project. Unlike GET .../feedback/stats, this endpoint returns raw, unaggregated rows
    /// ordered by created_at ascending.
    ///
    /// startDate and endDate are both optional, and each side defaults independently of the other
    /// when omitted (no ±N-days derivation like GET .../feedback/stats): a missing startDate
    /// defaults to the first day of the current UTC month (00:00 UTC), and a missing endDate
    /// defaults to now (UTC). The effective window (endDate - startDate, after defaults are
    /// applied) must not exceed 366 days (12 months); exceeding it is a 400, as is an effective
    /// startDate after endDate.
    /// </remarks>
    /// <param name="startDate">
    /// Inclusive lower bound (UTC, ISO 8601) of the export window. Optional - defaults to the
    /// first day of the current UTC month (00:00 UTC) when omitted.
    /// </param>
    /// <param name="endDate">
    /// Inclusive upper bound (UTC, ISO 8601) of the export window. Optional - defaults to now
    /// (UTC) when omitted.
    /// </param>
    /// <param name="projectId">
    /// Restrict the export to a single project, instead of all registered projects. Must
    /// correspond to a project id returned by the list_projects MCP tool; a projectId that does
    /// not match any project results in a 404.
    /// </param>
    /// <param name="timezone">
    /// IANA timezone name (e.g. "America/Sao_Paulo") used to render the created_at column of the
    /// CSV in that zone's local wall-clock time, with an explicit UTC offset suffix. Optional -
    /// when omitted, created_at keeps its UTC rendering (a "Z" suffix). Does not affect startDate/
    /// endDate filtering or the default-window rules, which stay UTC-based regardless. An
    /// unrecognized IANA name is a 400.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">
    /// CSV file with one row per feedback record in the requested window (and project, if given),
    /// ordered by created_at ascending.
    /// </response>
    /// <response code="400">
    /// startDate or endDate is not a valid date-time, the effective startDate (after defaults are
    /// applied) is after the effective endDate, the effective window exceeds 366 days, or timezone
    /// was given but is not a recognized IANA time zone name. This is the only client-error
    /// condition other than 404 under which this endpoint fails.
    /// </response>
    /// <response code="404">
    /// projectId was given but does not correspond to any registered project. This is the only
    /// condition under which this endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportAsync(
        [FromQuery(Name = "startDate")] DateTimeOffset? startDate,
        [FromQuery(Name = "endDate")] DateTimeOffset? endDate,
        [FromQuery(Name = "projectId")] Guid? projectId,
        [FromQuery(Name = "timezone")] string? timezone,
        CancellationToken cancellationToken)
    {
        TimeZoneInfo? tz = null;
        if (!string.IsNullOrEmpty(timezone) && !TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out tz))
        {
            return FeedbackFailures.InvalidTimezone(timezone).ToActionResult(HttpContext);
        }

        var result = await feedbackService.ExportAsync(
            startDate?.UtcDateTime,
            endDate?.UtcDateTime,
            projectId,
            cancellationToken);

        return result.Map(
            onSuccess: export => (IActionResult)File(FeedbackCsvExporter.ToCsvBytes(export.Rows, tz), "text/csv", FeedbackCsvExporter.ToFileName(export, projectId)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }
}
