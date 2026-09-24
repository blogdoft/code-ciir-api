using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Feedback;
using CodeCiir.Application.Projects;
using CsvHelper;
using NSubstitute;
using Shouldly;
using System.Globalization;
using System.Net;
using Xunit;

namespace CodeCiir.Api.Tests.Controllers.FeedbackControllerTests;

public sealed class ExportAsyncTests(CustomWebApplicationFactory factory) : BaseFeedbackControllerTests(factory)
{
    private const string ExportPath = "/api/code-queries/feedback/export";

    [Fact]
    public async Task Should_ReturnACsvAttachment_When_RequestIsValid()
    {
        GivenExport(Utc(2026, 1, 1), Utc(2026, 2, 1), projectId: 1, createdAt: Utc(2026, 1, 5, 12));
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{ExportPath}?startDate=2026-01-01T00:00:00Z&endDate=2026-02-01T00:00:00Z&projectId=1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType, response.Content.Headers.ContentDisposition?.DispositionType).ShouldBe(("text/csv", "attachment"));
        response.Content.Headers.ContentDisposition?.FileName.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_WriteOneRowPerFeedbackWithTheDocumentedColumns_When_RequestIsValid()
    {
        GivenExport(Utc(2026, 1, 1), Utc(2026, 2, 1), projectId: 1, createdAt: Utc(2026, 1, 5, 12));
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{ExportPath}?startDate=2026-01-01T00:00:00Z&endDate=2026-02-01T00:00:00Z&projectId=1");

        var row = (await ReadCsvRowsAsync(response)).ShouldHaveSingleItem();
        (row["project_id"], row["useful"], row["similarities"], row["created_at"]).ShouldBe(("1", "True", "[0.91,0.73]", "2026-01-05T12:00:00Z"));
    }

    [Fact]
    public async Task Should_RenderCreatedAtWithTheLocalOffset_When_TimezoneIsGiven()
    {
        GivenExport(null, null, projectId: null, createdAt: new DateTime(2018, 6, 5, 12, 0, 0, DateTimeKind.Utc));
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{ExportPath}?timezone=America/Sao_Paulo");

        var row = (await ReadCsvRowsAsync(response)).ShouldHaveSingleItem();
        row["created_at"].ShouldBe("2018-06-05T09:00:00-03:00");
    }

    [Fact]
    public async Task Should_ReturnBadRequestWithoutCallingTheService_When_TimezoneIsUnrecognized()
    {
        var failure = FeedbackFailures.InvalidTimezone("Not/AZone");
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{ExportPath}?timezone=Not/AZone");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync()).ShouldContain(failure.Message);
        await FeedbackService.DidNotReceive().ExportAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnBadRequestAsProblemDetails_When_StartDateIsAfterEndDate()
    {
        FeedbackService.ExportAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), null, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackExportResult>.FromFailure(FeedbackFailures.InvalidDateRange()));
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{ExportPath}?startDate=2026-06-01T00:00:00Z&endDate=2026-01-01T00:00:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithEmptyBody_When_ProjectDoesNotExist()
    {
        FeedbackService.ExportAsync(null, null, 999, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackExportResult>.FromFailure(ProjectFailures.ProjectNotFound(999)));
        using var client = CreateClient();

        using var response = await GetAsync(client, $"{ExportPath}?projectId=999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
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
            rows.Add(csv.HeaderRecord!.ToDictionary(header => header, header => csv.GetField(header) ?? string.Empty));
        }

        return rows;
    }

    private void GivenExport(DateTime? start, DateTime? end, long? projectId, DateTime createdAt)
    {
        var export = new FeedbackExportResult(
            start ?? DateTime.UtcNow,
            end ?? DateTime.UtcNow,
            [new FeedbackExportRow(1, 1, Faker.Commerce.ProductName(), Faker.Lorem.Sentence(), true, [0.91, 0.73], null, Agent, createdAt)]);
        FeedbackService.ExportAsync(start, end, projectId, Arg.Any<CancellationToken>())
            .Returns(Result<FeedbackExportResult>.FromSuccess(export));
    }
}
