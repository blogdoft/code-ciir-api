using Bogus;
using CodeCiir.Infrastructure.Database.CodeQueries;
using CodeCiir.Infrastructure.Database.Tests.Support;

namespace CodeCiir.Infrastructure.Database.Tests.CodeQueries.CodeDocumentsRepositoryTests;

public abstract class BaseCodeDocumentsRepositoryTests(PostgresFixture fixture)
{
    protected const string RawBaseUrl = "https://raw.githubusercontent.com/acme/widgets/main";

    protected const string SourcePath = "src/Widgets/Widget.cs";

    internal DatabaseSeeder Seeder { get; } = new(fixture.DataSource);

    protected Faker Faker { get; } = new();

    protected CodeDocumentsRepository Sut { get; } = new(fixture.DataSource);
}
