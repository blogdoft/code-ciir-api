using CodeCiir.Infrastructure.Database.CodeQueries;
using CodeCiir.Infrastructure.Database.Tests.Support;

namespace CodeCiir.Infrastructure.Database.Tests.CodeQueries.RelationshipGraphRepositoryTests;

public abstract class BaseRelationshipGraphRepositoryTests(PostgresFixture fixture)
{
    internal DatabaseSeeder Seeder { get; } = new(fixture.DataSource);

    protected RelationshipGraphRepository Sut { get; } = new(fixture.DataSource);
}
