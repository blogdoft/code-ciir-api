using Bogus;
using CodeCiir.Application.Projects;

namespace CodeCiir.Application.Tests.Support;

internal static class ProjectFaker
{
    private static readonly Faker<Project> Instance = new Faker<Project>()
        .CustomInstantiator(faker => new Project(
            faker.Random.Long(1, 1000),
            faker.Commerce.ProductName(),
            "bge-m3",
            1024,
            faker.Internet.UrlWithPath().ToUri(),
            faker.Internet.UrlWithPath().ToUri(),
            faker.Date.PastOffset().UtcDateTime,
            faker.Date.RecentOffset().UtcDateTime));

    public static Project Create() => Instance.Generate();

    public static List<Project> Create(int count) => Instance.Generate(count);

    private static Uri ToUri(this string url) => new(url);
}
