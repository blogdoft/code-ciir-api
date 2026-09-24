using Bogus;
using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Feedback;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using System.Net.Http.Json;
using Xunit;

namespace CodeCiir.Api.Tests.Controllers.CodeQueriesControllerTests;

public abstract class BaseCodeQueriesControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    protected const string CodeQueriesPath = "/api/code-queries";

    protected const string FeedbackPath = "/api/code-queries/feedback";

    protected ICodeQueryService CodeQueryService { get; } = Substitute.For<ICodeQueryService>();

    protected IFeedbackService FeedbackService { get; } = Substitute.For<IFeedbackService>();

    protected Faker Faker { get; } = new();

    protected static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body) =>
        client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body);

    protected HttpClient CreateClient() => factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICodeQueryService>();
            services.AddScoped(_ => CodeQueryService);
            services.RemoveAll<IFeedbackService>();
            services.AddScoped(_ => FeedbackService);
        }))
        .CreateClient();
}
