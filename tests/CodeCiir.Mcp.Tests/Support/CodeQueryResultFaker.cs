using Bogus;
using CodeCiir.Application.CodeQueries;

namespace CodeCiir.Mcp.Tests.Support;

internal static class CodeQueryResultFaker
{
    private static readonly Faker<CodeQueryResult> Instance = new Faker<CodeQueryResult>()
        .CustomInstantiator(faker => new CodeQueryResult(
            faker.Random.Long(1, 100_000),
            faker.PickRandom("method", "type", "property"),
            faker.Lorem.Word(),
            faker.Lorem.Word(),
            faker.Lorem.Word(),
            faker.Lorem.Word(),
            faker.System.FilePath(),
            faker.Lorem.Sentence(),
            faker.Random.Double(0.1, 1.0)));

    public static CodeQueryResult Create() => Instance.Generate();
}
