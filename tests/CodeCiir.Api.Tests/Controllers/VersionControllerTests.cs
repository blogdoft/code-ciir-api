using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeCiir.Api.Tests.Controllers;

public sealed class VersionControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Should_ReturnOkWithTheRunningVersion_When_Requested()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("version").GetString().ShouldStartWith("v");
    }
}
