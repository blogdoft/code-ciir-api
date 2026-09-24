using Shouldly;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace CodeCiir.Api.Tests.Extensions;

public sealed partial class SwaggerExtensionsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string SwaggerJsonPath = "/api/code-queries/swagger/v1/swagger.json";

    [Fact]
    public async Task Should_UseKebabCaseTagNames_When_TheDocumentIsGenerated()
    {
        using var document = await FetchDocumentAsync();

        var tags = document.RootElement.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString()!).ToList();
        tags.ShouldNotBeEmpty();
        tags.ShouldAllBe(tag => KebabCase().IsMatch(tag));
    }

    [Fact]
    public async Task Should_DescribeRequestPropertiesInCamelCase_When_TheDocumentIsGenerated()
    {
        using var document = await FetchDocumentAsync();

        var properties = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("CodeQueryRequest").GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();
        properties.ShouldBe(["question", "projectId", "minSimilarity", "kind", "qualifiedName", "limit"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_ProvideARequestExample_When_TheDocumentIsGenerated()
    {
        using var document = await FetchDocumentAsync();

        var example = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("CodeQueryRequest").GetProperty("example");
        example.GetProperty("question").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_DocumentInternalServerErrorWithoutABody_When_TheDocumentIsGenerated()
    {
        using var document = await FetchDocumentAsync();

        var response = document.RootElement.GetProperty("paths").GetProperty("/api/code-queries")
            .GetProperty("post").GetProperty("responses").GetProperty("500");
        response.TryGetProperty("content", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_DocumentTheCsvExportContentType_When_TheDocumentIsGenerated()
    {
        using var document = await FetchDocumentAsync();

        var content = document.RootElement.GetProperty("paths").GetProperty("/api/code-queries/feedback/export")
            .GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content");
        content.TryGetProperty("text/csv", out _).ShouldBeTrue();
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KebabCase();

    private async Task<JsonDocument> FetchDocumentAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(SwaggerJsonPath, UriKind.Relative));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
