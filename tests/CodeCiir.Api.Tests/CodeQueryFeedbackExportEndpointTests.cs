using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Feedback;
using CsvHelper;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using System.Globalization;
using System.Net;
using Xunit;

namespace CodeCiir.Api.Tests;

public sealed class CodeQueryFeedbackExportEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task ExportAsync_Valid_ReturnsCsvFile()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var export = new FeedbackExportResult(
            start,
            end,
            [new FeedbackExportRow(1, 1, "proj", "why is this slow?", true, [0.91, 0.73], null, "claude code", new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc))]);
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.ExportAsync(start, end, 1, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackExportResult>.FromSuccess(export));

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(
            new Uri("/api/code-queries/feedback/export?start_date=2026-01-01T00:00:00Z&end_date=2026-02-01T00:00:00Z&project_id=1", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition?.FileName.ShouldNotBeNullOrEmpty();

        var rows = await ReadCsvRowsAsync(response);
        rows.ShouldHaveSingleItem();
        rows[0]["project_id"].ShouldBe("1");
        rows[0]["useful"].ShouldBe("True");
        rows[0]["similarities"].ShouldBe("[0.91,0.73]");
        rows[0]["created_at"].ShouldBe("2026-01-05T12:00:00Z");
    }

    [Fact]
    public async Task ExportAsync_TimezoneGiven_RendersCreatedAtWithLocalOffset()
    {
        var export = new FeedbackExportResult(
            DateTime.UtcNow,
            DateTime.UtcNow,
            [new FeedbackExportRow(1, 1, "proj", "question", true, [], null, "claude code", new DateTime(2018, 6, 5, 12, 0, 0, DateTimeKind.Utc))]);
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.ExportAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackExportResult>.FromSuccess(export));

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(
            new Uri("/api/code-queries/feedback/export?timezone=America/Sao_Paulo", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rows = await ReadCsvRowsAsync(response);
        rows[0]["created_at"].ShouldBe("2018-06-05T09:00:00-03:00");
    }

    [Fact]
    public async Task ExportAsync_UnrecognizedTimezone_ReturnsBadRequestWithoutCallingService()
    {
        var feedbackService = Substitute.For<IFeedbackService>();

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(new Uri("/api/code-queries/feedback/export?timezone=Not/AZone", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        await feedbackService.DidNotReceive().ExportAsync(
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_InvalidDateRange_ReturnsBadRequest()
    {
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.ExportAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackExportResult>.FromFailure(FeedbackFailures.InvalidDateRange()));

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(
            new Uri("/api/code-queries/feedback/export?start_date=2026-06-01T00:00:00Z&end_date=2026-01-01T00:00:00Z", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task ExportAsync_ProjectNotFound_ReturnsNotFound()
    {
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.ExportAsync(null, null, 999, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackExportResult>.FromFailure(
                CodeCiir.Application.Projects.ProjectFailures.ProjectNotFound(999)));

        using var client = CreateClient(feedbackService);
        using var response = await client.GetAsync(new Uri("/api/code-queries/feedback/export?project_id=999", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsByteArrayAsync();
        content.ShouldBeEmpty();
    }

    private static async Task<IReadOnlyList<Dictionary<string, string>>> ReadCsvRowsAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var rows = new List<Dictionary<string, string>>();
        await csv.ReadAsync();
        csv.ReadHeader();
        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, string>();
            foreach (var header in csv.HeaderRecord!)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    private HttpClient CreateClient(IFeedbackService feedbackService) => factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFeedbackService>();
            services.AddScoped(_ => feedbackService);
        }))
        .CreateClient();
}
