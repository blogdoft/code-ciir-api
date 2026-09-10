using Shouldly;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CodeCiir.Api.Tests;

public sealed class VersionEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task GetAsync_ReturnsOkWithVersion()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VersionResponseDto>();
        body.ShouldNotBeNull();
        body.Version.ShouldNotBeNullOrWhiteSpace();
    }

    private sealed record VersionResponseDto(string Version);
}
