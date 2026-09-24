using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.Projects.ProjectsRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class SearchAsyncTests(PostgresFixture fixture) : BaseProjectsRepositoryTests(fixture)
{
    [Fact]
    public async Task Should_IncludeTheInsertedProject_When_NoFilterIsGiven()
    {
        var name = $"proj-{Guid.NewGuid():N}";
        await Seeder.InsertProjectAsync(name);

        var (items, _) = await Sut.SearchAsync(null, 0, 100);

        items.ShouldContain(p => p.Name == name);
    }

    [Fact]
    public async Task Should_MatchTheProject_When_FilterIsPartialAndCaseInsensitive()
    {
        var uniqueToken = Guid.NewGuid().ToString("N");
        var name = $"Billing-{uniqueToken}-Service";
        await Seeder.InsertProjectAsync(name);

        var (items, totalCount) = await Sut.SearchAsync(uniqueToken.ToUpperInvariant(), 0, 100);

        items.Select(p => p.Name).ShouldBe([name]);
        totalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Should_ReturnNothing_When_FilterMatchesNoProject()
    {
        var (items, totalCount) = await Sut.SearchAsync($"no-such-project-{Guid.NewGuid():N}", 0, 100);

        items.ShouldBeEmpty();
        totalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_SkipFirstPageItems_When_SecondPageIsRequested()
    {
        var uniqueToken = Guid.NewGuid().ToString("N");
        foreach (var suffix in new[] { "a", "b", "c" })
        {
            await Seeder.InsertProjectAsync($"page-{uniqueToken}-{suffix}");
        }

        var (firstPage, totalCount) = await Sut.SearchAsync(uniqueToken, 0, 2);
        var (secondPage, _) = await Sut.SearchAsync(uniqueToken, 1, 2);

        totalCount.ShouldBe(3);
        (firstPage.Count, secondPage.Count).ShouldBe((2, 1));
        firstPage.Select(p => p.Name).ShouldNotContain(secondPage[0].Name);
    }
}
