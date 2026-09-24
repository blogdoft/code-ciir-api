using Bogus;
using CodeCiir.Application.Feedback;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Xunit;

namespace CodeCiir.Api.Tests.Controllers.FeedbackControllerTests;

public abstract class BaseFeedbackControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    protected const string Agent = "claude code";

    protected IFeedbackService FeedbackService { get; } = Substitute.For<IFeedbackService>();

    protected Faker Faker { get; } = new();

    protected static DateTime Utc(int year, int month, int day, int hour = 0) => new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    protected static Task<HttpResponseMessage> GetAsync(HttpClient client, string pathAndQuery) =>
        client.GetAsync(new Uri(pathAndQuery, UriKind.Relative));

    protected HttpClient CreateClient() => factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFeedbackService>();
            services.AddScoped(_ => FeedbackService);
        }))
        .CreateClient();
}
