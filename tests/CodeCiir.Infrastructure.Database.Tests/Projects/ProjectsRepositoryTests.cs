using CodeCiir.Infrastructure.Database.Projects;
using Dapper;
using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.Projects;

[Collection(PostgresCollection.Name)]
public sealed class ProjectsRepositoryTests(PostgresFixture fixture)
{
    private readonly ProjectsRepository _sut = new(fixture.DataSource);

    [Fact]
    public async Task GetByIdAsync_ExistingProject_ReturnsIt()
    {
        var id = await InsertProjectAsync($"proj-{Guid.NewGuid():N}");

        var project = await _sut.GetByIdAsync(id);

        project.ShouldNotBeNull();
        project.Id.ShouldBe(id);
        project.EmbeddingModel.ShouldBe("bge-m3");
        project.EmbeddingDimensions.ShouldBe(1024);
    }

    [Fact]
    public async Task GetByIdAsync_MissingProject_ReturnsNull()
    {
        var project = await _sut.GetByIdAsync(-1);

        project.ShouldBeNull();
    }

    [Fact]
    public async Task SearchAsync_NoFilter_IncludesInsertedProject()
    {
        var name = $"proj-{Guid.NewGuid():N}";
        await InsertProjectAsync(name);

        var (items, _) = await _sut.SearchAsync(null, 0, 100);

        items.ShouldContain(p => p.Name == name);
    }

    [Fact]
    public async Task SearchAsync_PartialCaseInsensitiveFilter_MatchesProject()
    {
        var uniqueToken = Guid.NewGuid().ToString("N");
        var name = $"Billing-{uniqueToken}-Service";
        await InsertProjectAsync(name);

        var (items, totalCount) = await _sut.SearchAsync(uniqueToken.ToUpperInvariant(), 0, 100);

        items.ShouldContain(p => p.Name == name);
        totalCount.ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_FilterMatchingNothing_ReturnsEmpty()
    {
        var (items, totalCount) = await _sut.SearchAsync($"no-such-project-{Guid.NewGuid():N}", 0, 100);

        items.ShouldBeEmpty();
        totalCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_SecondPage_SkipsFirstPageItems()
    {
        var uniqueToken = Guid.NewGuid().ToString("N");
        var names = new[] { $"page-{uniqueToken}-a", $"page-{uniqueToken}-b", $"page-{uniqueToken}-c" };
        foreach (var name in names)
        {
            await InsertProjectAsync(name);
        }

        var (firstPage, totalCount) = await _sut.SearchAsync(uniqueToken, 0, 2);
        var (secondPage, _) = await _sut.SearchAsync(uniqueToken, 1, 2);

        totalCount.ShouldBe(3);
        firstPage.Count.ShouldBe(2);
        secondPage.Count.ShouldBe(1);
        firstPage.Select(p => p.Name).ShouldNotContain(secondPage[0].Name);
    }

    private async Task<long> InsertProjectAsync(string name)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO public.projects (name, embedding_model, embedding_dimensions)
            VALUES (@Name, 'bge-m3', 1024)
            RETURNING id
            """,
            new { Name = name });
    }
}
