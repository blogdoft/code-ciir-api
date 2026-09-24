using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.Projects.ProjectsRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class GetByPublicIdAsyncTests(PostgresFixture fixture) : BaseProjectsRepositoryTests(fixture)
{
    [Fact]
    public async Task Should_ReturnTheProject_When_ItExists()
    {
        var publicId = Guid.NewGuid();
        await Seeder.InsertProjectAsync(embeddingModel: "bge-m3", embeddingDimensions: 1024, publicId: publicId);

        var project = await Sut.GetByPublicIdAsync(publicId);

        project.ShouldNotBeNull();
        project.PublicId.ShouldBe(publicId);
        project.EmbeddingModel.ShouldBe("bge-m3");
        project.EmbeddingDimensions.ShouldBe(1024);
    }

    [Fact]
    public async Task Should_StampTimestampsAsUtc_When_ProjectIsRead()
    {
        var publicId = Guid.NewGuid();
        await Seeder.InsertProjectAsync(publicId: publicId);

        var project = await Sut.GetByPublicIdAsync(publicId);

        project.ShouldNotBeNull();
        project.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        project.UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Should_ReturnNull_When_ProjectDoesNotExist()
    {
        var project = await Sut.GetByPublicIdAsync(Guid.NewGuid());

        project.ShouldBeNull();
    }
}
