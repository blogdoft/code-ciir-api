using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeCiir.Api.Tests.Filters;

/// <summary>
/// Covers <c>ModelValidationFilter</c> - the shared 400 path for a request body ASP.NET's own
/// model binding/JSON deserialization rejects before any controller action runs. Deliberately
/// exercised through one real endpoint (code-queries) rather than duplicated per controller: the
/// filter is global, so its behavior doesn't vary by endpoint.
/// </summary>
public sealed class ModelValidationFilterTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Should_ReportWhichFieldAndWhy_When_AFieldHasTheWrongJsonType()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries", UriKind.Relative),
            new { question = "question", projectId = "not-a-number" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.EnumerateObject().ShouldContain(e => e.Name.Contains("projectId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_ReportWhichFieldAndWhy_When_AnUnknownPropertyIsSent()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries", UriKind.Relative),
            new { question = "question", notARealField = 123 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.EnumerateObject().ShouldContain(e => e.Name.Contains("notARealField", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_ReportWhichFieldAndWhy_When_ANestedObjectMissesARequiredProperty()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries", UriKind.Relative),
            new { question = "question", qualifiedName = new { value = "Foo" } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.EnumerateObject().ShouldContain(e => e.Name.Contains("qualifiedName", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_ReturnProblemJsonWithGenericDetail_When_TheBodyIsInvalid()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/code-queries", UriKind.Relative),
            new { question = "question", projectId = "not-a-number" });

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        (body.GetProperty("type").GetString(), body.GetProperty("title").GetString(), body.GetProperty("status").GetInt32(), body.GetProperty("instance").GetString())
            .ShouldBe(("https://httpstatuses.io/400", "Bad Request", 400, "/api/code-queries"));
    }
}
