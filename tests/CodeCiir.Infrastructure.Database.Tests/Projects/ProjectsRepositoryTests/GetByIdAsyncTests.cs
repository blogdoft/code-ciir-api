using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.Projects.ProjectsRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class GetByIdAsyncTests(PostgresFixture fixture) : BaseProjectsRepositoryTests(fixture)
{
    [Fact]
    public async Task Should_ReturnTheProject_When_ItExists()
    {
        var id = await Seeder.InsertProjectAsync(embeddingModel: "bge-m3", embeddingDimensions: 1024);

        var project = await Sut.GetByIdAsync(id);

        project.ShouldNotBeNull();
        project.Id.ShouldBe(id);
        project.EmbeddingModel.ShouldBe("bge-m3");
        project.EmbeddingDimensions.ShouldBe(1024);
    }

    [Fact]
    public async Task Should_StampTimestampsAsUtc_When_ProjectIsRead()
    {
        var id = await Seeder.InsertProjectAsync();

        var project = await Sut.GetByIdAsync(id);

        project.ShouldNotBeNull();
        project.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        project.UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Should_ReturnNull_When_ProjectDoesNotExist()
    {
        var project = await Sut.GetByIdAsync(-1);

        project.ShouldBeNull();
    }
}
