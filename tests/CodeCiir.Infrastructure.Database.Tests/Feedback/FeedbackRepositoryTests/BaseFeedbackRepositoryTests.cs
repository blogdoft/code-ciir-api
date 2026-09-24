using Bogus;
using CodeCiir.Infrastructure.Database.Feedback;
using CodeCiir.Infrastructure.Database.Tests.Support;

namespace CodeCiir.Infrastructure.Database.Tests.Feedback.FeedbackRepositoryTests;

public abstract class BaseFeedbackRepositoryTests(PostgresFixture fixture)
{
    internal DatabaseSeeder Seeder { get; } = new(fixture.DataSource);

    protected Faker Faker { get; } = new();

    protected FeedbackRepository Sut { get; } = new(fixture.DataSource);

    // Dates in these tests are fixed, far in the past, on purpose: no other test in the shared
    // Postgres collection can then pollute the windows they query.
    protected static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);
}
