using Bogus;
using CodeCiir.Application.Projects;

namespace CodeCiir.Mcp.Tests.Support;

internal static class ProjectFaker
{
    private static readonly Faker<Project> Instance = new Faker<Project>()
        .CustomInstantiator(faker => new Project(
            faker.Random.Long(1, 1000),
            faker.Random.Guid(),
            faker.Commerce.ProductName(),
            "bge-m3",
            1024,
            new Uri(faker.Internet.UrlWithPath()),
            new Uri(faker.Internet.UrlWithPath()),
            faker.Date.PastOffset().UtcDateTime,
            faker.Date.RecentOffset().UtcDateTime));

    public static Project Create() => Instance.Generate();
}
