using Bogus;
using CodeCiir.Infrastructure.Database.Projects;
using CodeCiir.Infrastructure.Database.Tests.Support;

namespace CodeCiir.Infrastructure.Database.Tests.Projects.ProjectsRepositoryTests;

public abstract class BaseProjectsRepositoryTests(PostgresFixture fixture)
{
    internal DatabaseSeeder Seeder { get; } = new(fixture.DataSource);

    protected Faker Faker { get; } = new();

    protected ProjectsRepository Sut { get; } = new(fixture.DataSource);
}
