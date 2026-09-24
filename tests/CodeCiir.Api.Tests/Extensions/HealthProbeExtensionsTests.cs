using Shouldly;
using System.Net;
using Xunit;

namespace CodeCiir.Api.Tests.Extensions;

public sealed class HealthProbeExtensionsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Should_ReturnOkWithEmptyBody_When_HealthIsRequested()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }
}
