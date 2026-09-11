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

    [Fact]
    public async Task ExistsByNameAsync_ExistingName_ReturnsTrue()
    {
        var name = $"proj-{Guid.NewGuid():N}";
        await InsertProjectAsync(name);

        var exists = await _sut.ExistsByNameAsync(name, excludingId: null);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_MissingName_ReturnsFalse()
    {
        var exists = await _sut.ExistsByNameAsync($"no-such-project-{Guid.NewGuid():N}", excludingId: null);

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByNameAsync_ExcludingOwnId_ReturnsFalse()
    {
        var name = $"proj-{Guid.NewGuid():N}";
        var id = await InsertProjectAsync(name);

        var exists = await _sut.ExistsByNameAsync(name, excludingId: id);

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task InsertAsync_PersistsAndReturnsProject()
    {
        var name = $"proj-{Guid.NewGuid():N}";

        var project = await _sut.InsertAsync(
            name,
            "bge-m3",
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        project.Id.ShouldBeGreaterThan(0);
        project.Name.ShouldBe(name);
        project.EmbeddingModel.ShouldBe("bge-m3");
        project.EmbeddingDimensions.ShouldBe(1024);

        var reloaded = await _sut.GetByIdAsync(project.Id);
        reloaded.ShouldNotBeNull();
        reloaded.Name.ShouldBe(name);
    }

    [Fact]
    public async Task UpdateAsync_ExistingProject_PersistsNewFields()
    {
        var id = await InsertProjectAsync($"proj-{Guid.NewGuid():N}");
        var newName = $"renamed-{Guid.NewGuid():N}";

        var updated = await _sut.UpdateAsync(
            id,
            newName,
            "bge-m3-v2",
            768,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        updated.ShouldNotBeNull();
        updated.Name.ShouldBe(newName);
        updated.EmbeddingModel.ShouldBe("bge-m3-v2");
        updated.EmbeddingDimensions.ShouldBe(768);

        var reloaded = await _sut.GetByIdAsync(id);
        reloaded.ShouldNotBeNull();
        reloaded.Name.ShouldBe(newName);
    }

    [Fact]
    public async Task UpdateAsync_MissingProject_ReturnsNull()
    {
        var updated = await _sut.UpdateAsync(
            -1,
            "proj",
            "bge-m3",
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        updated.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ExistingProject_RemovesItAndReturnsTrue()
    {
        var id = await InsertProjectAsync($"proj-{Guid.NewGuid():N}");

        var deleted = await _sut.DeleteAsync(id);

        deleted.ShouldBeTrue();
        (await _sut.GetByIdAsync(id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_MissingProject_ReturnsFalse()
    {
        var deleted = await _sut.DeleteAsync(-1);

        deleted.ShouldBeFalse();
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
