using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.CodeQueries.CodeDocumentsRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class GetSourceAsyncTests(PostgresFixture fixture) : BaseCodeDocumentsRepositoryTests(fixture)
{
    [Fact]
    public async Task Should_ReturnPathAndCalculatedRawUrl_When_DocumentExists()
    {
        var projectId = await Seeder.InsertProjectAsync(gitRawUrl: RawBaseUrl + "/");
        var documentId = await Seeder.InsertDocumentAsync(projectId, "widget", [1f, 0f, 0f], sourcePath: SourcePath);

        var result = await Sut.GetSourceAsync(documentId);

        result.ShouldNotBeNull();
        (result.DocumentId, result.SourceFile, result.GitRawUrl).ShouldBe((documentId, SourcePath, new Uri($"{RawBaseUrl}/{SourcePath}")));
    }

    [Theory]
    [InlineData("https://raw.githubusercontent.com/acme/widgets/main", "src/Widgets/Widget.cs")]
    [InlineData("https://raw.githubusercontent.com/acme/widgets/main/", "/src/Widgets/Widget.cs")]
    public async Task Should_SeparateRawUrlAndSourcePathWithExactlyOneSlash_When_EitherHasASlashAtTheJoin(string gitRawUrl, string sourcePath)
    {
        var projectId = await Seeder.InsertProjectAsync(gitRawUrl: gitRawUrl);
        var documentId = await Seeder.InsertDocumentAsync(projectId, "widget", [1f, 0f, 0f], sourcePath: sourcePath);

        var result = await Sut.GetSourceAsync(documentId);

        result.ShouldNotBeNull();
        result.GitRawUrl.ShouldBe(new Uri($"{RawBaseUrl}/{SourcePath}"));
    }

    [Fact]
    public async Task Should_ReturnNull_When_DocumentDoesNotExist()
    {
        var result = await Sut.GetSourceAsync(987654321);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_ReturnSourceFileWithNullRawUrl_When_ProjectHasNoRawUrl()
    {
        var projectId = await Seeder.InsertProjectAsync(gitRawUrl: null);
        var documentId = await Seeder.InsertDocumentAsync(projectId, "without-raw-url", [1f, 0f, 0f], sourcePath: "src/Widget.cs");

        var result = await Sut.GetSourceAsync(documentId);

        result.ShouldNotBeNull();
        (result.SourceFile, result.GitRawUrl).ShouldBe(("src/Widget.cs", null));
    }

    [Fact]
    public async Task Should_ReturnNullSourceFileAndRawUrl_When_DocumentHasNoSourcePath()
    {
        var projectId = await Seeder.InsertProjectAsync(gitRawUrl: RawBaseUrl);
        var documentId = await Seeder.InsertDocumentAsync(projectId, "without-path", [1f, 0f, 0f], sourcePath: null);

        var result = await Sut.GetSourceAsync(documentId);

        result.ShouldNotBeNull();
        (result.SourceFile, result.GitRawUrl).ShouldBe((null, null));
    }
}
