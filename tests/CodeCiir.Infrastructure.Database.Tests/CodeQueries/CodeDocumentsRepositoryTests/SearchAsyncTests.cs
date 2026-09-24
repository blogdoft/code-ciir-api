using CodeCiir.Application.CodeQueries;
using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.CodeQueries.CodeDocumentsRepositoryTests;

[Collection(PostgresCollection.Name)]
public sealed class SearchAsyncTests(PostgresFixture fixture) : BaseCodeDocumentsRepositoryTests(fixture)
{
    private static readonly float[] Query = [1f, 0f, 0f];

    [Fact]
    public async Task Should_OrderByDescendingSimilarity_When_SeveralDocumentsMatch()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var closeId = await Seeder.InsertDocumentAsync(projectId, "close", [1f, 0f, 0f]);
        var farId = await Seeder.InsertDocumentAsync(projectId, "far", [0f, 1f, 0f]);

        var results = (await Sut.SearchAsync(Query, null, projectId, null, null, null, limit: 10)).ToList();

        results.Select(r => r.Id).ShouldBe([closeId, farId]);
        results[0].Similarity.ShouldBeGreaterThan(results[1].Similarity);
    }

    [Fact]
    public async Task Should_ExcludeTheDocument_When_ItsEmbeddingIsNull()
    {
        var projectId = await Seeder.InsertProjectAsync();
        await Seeder.InsertDocumentAsync(projectId, "no-embedding", embedding: null);
        var withEmbeddingId = await Seeder.InsertDocumentAsync(projectId, "has-embedding", [1f, 0f, 0f]);

        var results = await Sut.SearchAsync(Query, null, projectId, null, null, null, limit: 10);

        results.Select(r => r.Id).ShouldBe([withEmbeddingId]);
    }

    [Fact]
    public async Task Should_FilterOutLowMatches_When_MinSimilarityIsGiven()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var closeId = await Seeder.InsertDocumentAsync(projectId, "close", [1f, 0f, 0f]);
        await Seeder.InsertDocumentAsync(projectId, "orthogonal", [0f, 1f, 0f]);

        var results = await Sut.SearchAsync(Query, 0.99, projectId, null, null, null, limit: 10);

        results.Select(r => r.Id).ShouldBe([closeId]);
    }

    [Fact]
    public async Task Should_ExcludeOtherProjectsDocuments_When_ProjectIdIsGiven()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var otherProjectId = await Seeder.InsertProjectAsync();
        await Seeder.InsertDocumentAsync(otherProjectId, "other-project-doc", [1f, 0f, 0f]);

        var results = await Sut.SearchAsync(Query, null, projectId, null, null, null, limit: 10);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_IncludeDocumentsFromEveryProject_When_ProjectIdIsOmitted()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var otherProjectId = await Seeder.InsertProjectAsync();
        var id = await Seeder.InsertDocumentAsync(projectId, "doc", [1f, 0f, 0f]);
        var otherId = await Seeder.InsertDocumentAsync(otherProjectId, "other-doc", [1f, 0f, 0f]);

        // The whole test database is searched, so the limit must be large enough that documents
        // seeded by other tests cannot push these two out of the result set.
        var results = await Sut.SearchAsync(Query, null, null, null, null, null, limit: 10_000);

        new[] { id, otherId }.ShouldBeSubsetOf(results.Select(r => r.Id).ToList());
    }

    [Fact]
    public async Task Should_ReturnNothing_When_ProjectHasNoDocuments()
    {
        var projectId = await Seeder.InsertProjectAsync();

        var results = await Sut.SearchAsync(Query, null, projectId, null, null, null, limit: 10);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_MatchOnlyExactKind_When_KindFilterIsGiven()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var methodId = await Seeder.InsertDocumentAsync(projectId, "m", [1f, 0f, 0f], kind: "method");
        await Seeder.InsertDocumentAsync(projectId, "t", [1f, 0f, 0f], kind: "type");

        var results = await Sut.SearchAsync(Query, null, projectId, "method", null, null, limit: 10);

        results.Select(r => r.Id).ShouldBe([methodId]);
    }

    [Fact]
    public async Task Should_MatchExactlyOnly_When_QualifiedNameOperatorIsEquals()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var exactId = await Seeder.InsertDocumentAsync(projectId, "a", [1f, 0f, 0f], qualifiedName: "Foo.Bar.Baz");
        await Seeder.InsertDocumentAsync(projectId, "b", [1f, 0f, 0f], qualifiedName: "Foo.Bar.BazQux");

        var results = await Sut.SearchAsync(
            Query, null, projectId, null, QualifiedNameFilterOperator.Equals, "Foo.Bar.Baz", limit: 10);

        results.Select(r => r.Id).ShouldBe([exactId]);
    }

    [Fact]
    public async Task Should_MatchThePattern_When_QualifiedNameContainsHasAWildcard()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var matchId = await Seeder.InsertDocumentAsync(projectId, "a", [1f, 0f, 0f], qualifiedName: "Foo.Bar.Baz");
        await Seeder.InsertDocumentAsync(projectId, "b", [1f, 0f, 0f], qualifiedName: "Foo.Qux.Baz");

        var results = await Sut.SearchAsync(
            Query, null, projectId, null, QualifiedNameFilterOperator.Contains, "*Bar*", limit: 10);

        results.Select(r => r.Id).ShouldBe([matchId]);
    }

    [Fact]
    public async Task Should_MatchExactlyIgnoringCase_When_QualifiedNameContainsHasNoWildcard()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var matchId = await Seeder.InsertDocumentAsync(projectId, "a", [1f, 0f, 0f], qualifiedName: "Foo.Bar.Baz");
        await Seeder.InsertDocumentAsync(projectId, "b", [1f, 0f, 0f], qualifiedName: "Foo.Bar.BazQux");

        var results = await Sut.SearchAsync(
            Query, null, projectId, null, QualifiedNameFilterOperator.Contains, "foo.bar.baz", limit: 10);

        results.Select(r => r.Id).ShouldBe([matchId]);
    }

    [Fact]
    public async Task Should_ExcludeMatchingDocuments_When_QualifiedNameOperatorIsNotContains()
    {
        var projectId = await Seeder.InsertProjectAsync();
        await Seeder.InsertDocumentAsync(projectId, "a", [1f, 0f, 0f], qualifiedName: "Foo.Bar.Baz");
        var keptId = await Seeder.InsertDocumentAsync(projectId, "b", [1f, 0f, 0f], qualifiedName: "Foo.Qux.Baz");

        var results = await Sut.SearchAsync(
            Query, null, projectId, null, QualifiedNameFilterOperator.NotContains, "*Bar*", limit: 10);

        results.Select(r => r.Id).ShouldBe([keptId]);
    }

    [Fact]
    public async Task Should_ReturnOnlyTheClosestMatches_When_LimitIsLowerThanTheMatchCount()
    {
        var projectId = await Seeder.InsertProjectAsync();
        var closestId = await Seeder.InsertDocumentAsync(projectId, "closest", [1f, 0f, 0f]);
        await Seeder.InsertDocumentAsync(projectId, "middle", [0.9f, 0.1f, 0f]);
        await Seeder.InsertDocumentAsync(projectId, "farthest", [0f, 1f, 0f]);

        var results = await Sut.SearchAsync(Query, null, projectId, null, null, null, limit: 1);

        results.Select(r => r.Id).ShouldBe([closestId]);
    }

    [Fact]
    public async Task Should_ReturnTheProjectGitUrlAndRawUrlConcatenatedWithSourcePath_When_ProjectHasThem()
    {
        const string gitUrl = "https://github.com/acme/widgets";
        var projectId = await Seeder.InsertProjectAsync(gitUrl: gitUrl, gitRawUrl: RawBaseUrl + "/");
        await Seeder.InsertDocumentAsync(projectId, "widget", [1f, 0f, 0f], sourcePath: SourcePath);

        var result = (await Sut.SearchAsync(Query, null, projectId, null, null, null, limit: 10)).Single();

        (result.GitUrl, result.GitRawUrl).ShouldBe((new Uri(gitUrl), new Uri($"{RawBaseUrl}/{SourcePath}")));
    }

    [Fact]
    public async Task Should_ReturnNullRawUrl_When_ProjectRawUrlIsNull()
    {
        var projectId = await Seeder.InsertProjectAsync(gitRawUrl: null);
        await Seeder.InsertDocumentAsync(projectId, "widget", [1f, 0f, 0f], sourcePath: SourcePath);

        var result = (await Sut.SearchAsync(Query, null, projectId, null, null, null, limit: 10)).Single();

        result.GitRawUrl.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnNullUrls_When_ProjectUrlsAreEmpty(string emptyUrl)
    {
        var projectId = await Seeder.InsertProjectAsync(gitUrl: emptyUrl, gitRawUrl: emptyUrl);
        await Seeder.InsertDocumentAsync(projectId, "widget", [1f, 0f, 0f], sourcePath: SourcePath);

        var result = (await Sut.SearchAsync(Query, null, projectId, null, null, null, limit: 10)).Single();

        (result.GitUrl, result.GitRawUrl).ShouldBe((null, null));
    }

    [Theory]
    [InlineData("https://raw.githubusercontent.com/acme/widgets/main", "src/Widgets/Widget.cs")]
    [InlineData("https://raw.githubusercontent.com/acme/widgets/main/", "/src/Widgets/Widget.cs")]
    public async Task Should_SeparateRawUrlAndSourcePathWithExactlyOneSlash_When_EitherHasASlashAtTheJoin(string gitRawUrl, string sourcePath)
    {
        var projectId = await Seeder.InsertProjectAsync(gitRawUrl: gitRawUrl);
        await Seeder.InsertDocumentAsync(projectId, "widget", [1f, 0f, 0f], sourcePath: sourcePath);

        var result = (await Sut.SearchAsync(Query, null, projectId, null, null, null, limit: 10)).Single();

        result.GitRawUrl.ShouldBe(new Uri($"{RawBaseUrl}/{SourcePath}"));
    }
}
