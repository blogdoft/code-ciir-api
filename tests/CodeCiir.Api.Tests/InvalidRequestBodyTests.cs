using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeCiir.Api.Tests;

/// <summary>
/// Covers <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c> (Program.cs) - the shared
/// 400 path for a request body ASP.NET's own model binding/JSON deserialization rejects before
/// any controller action runs. This is deliberately exercised through one real endpoint
/// (code-queries) rather than duplicated per-controller: the factory is global, so its behavior
/// doesn't vary by endpoint.
/// </summary>
public sealed class InvalidRequestBodyTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task PostAsync_FieldWithWrongJsonType_ReportsWhichFieldAndWhy()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/code-queries", UriKind.Relative),
            new { question = "question", project_id = "not-a-number" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");
        errors.EnumerateObject().ShouldContain(e => e.Name.Contains("project_id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PostAsync_UnknownProperty_ReportsWhichFieldAndWhy()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/code-queries", UriKind.Relative),
            new { question = "question", not_a_real_field = 123 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");
        errors.EnumerateObject().ShouldContain(e => e.Name.Contains("not_a_real_field", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PostAsync_NestedObjectMissingRequiredProperty_ReportsWhichFieldAndWhy()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/code-queries", UriKind.Relative),
            new { question = "question", qualified_name = new { value = "Foo" } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");
        errors.EnumerateObject().ShouldContain(e => e.Name.Contains("qualified_name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PostAsync_InvalidBody_ResponseIsStillProblemJsonWithGenericDetail()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/code-queries", UriKind.Relative),
            new { question = "question", project_id = "not-a-number" });

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().ShouldBe("https://httpstatuses.io/400");
        body.GetProperty("title").GetString().ShouldBe("Bad Request");
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("instance").GetString().ShouldBe("/api/v1/code-queries");
    }
}
