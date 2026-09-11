using CodeCiir.Application.CodeQueries;
using CodeCiir.Infrastructure.Database.CodeQueries;
using Dapper;
using Pgvector;
using Shouldly;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests.CodeQueries;

[Collection(PostgresCollection.Name)]
public sealed class CodeDocumentsRepositoryTests(PostgresFixture fixture)
{
    private readonly CodeDocumentsRepository _sut = new(fixture.DataSource);

    [Fact]
    public async Task SearchAsync_OrdersByDescendingSimilarity()
    {
        var projectId = await InsertProjectAsync();
        var closeId = await InsertDocumentAsync(projectId, "close", [1f, 0f, 0f]);
        var farId = await InsertDocumentAsync(projectId, "far", [0f, 1f, 0f]);

        var results = (await _sut.SearchAsync([1f, 0f, 0f], null, projectId, null, null, null, limit: 10)).ToList();

        results.Select(r => r.Id).ShouldBe([closeId, farId]);
        results[0].Similarity.ShouldBeGreaterThan(results[1].Similarity);
    }

    [Fact]
    public async Task SearchAsync_DocumentWithNullEmbedding_IsExcluded()
    {
        var projectId = await InsertProjectAsync();
        await InsertDocumentAsync(projectId, "no-embedding", embedding: null);
        var withEmbeddingId = await InsertDocumentAsync(projectId, "has-embedding", [1f, 0f, 0f]);

        var results = await _sut.SearchAsync([1f, 0f, 0f], null, projectId, null, null, null, limit: 10);

        results.Select(r => r.Id).ShouldBe([withEmbeddingId]);
    }

    [Fact]
    public async Task SearchAsync_MinSimilarityFiltersOutLowMatches()
    {
        var projectId = await InsertProjectAsync();
        await InsertDocumentAsync(projectId, "close", [1f, 0f, 0f]);
        await InsertDocumentAsync(projectId, "orthogonal", [0f, 1f, 0f]);

        var results = await _sut.SearchAsync([1f, 0f, 0f], 0.99, projectId, null, null, null, limit: 10);

        results.Count().ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_ProjectIdGiven_ExcludesOtherProjectsDocuments()
    {
        var projectId = await InsertProjectAsync();
        var otherProjectId = await InsertProjectAsync();
        await InsertDocumentAsync(otherProjectId, "other-project-doc", [1f, 0f, 0f]);

        var results = await _sut.SearchAsync([1f, 0f, 0f], null, projectId, null, null, null, limit: 10);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ProjectIdOmitted_IncludesDocumentsFromEveryProject()
    {
        var projectId = await InsertProjectAsync();
        var otherProjectId = await InsertProjectAsync();
        var id = await InsertDocumentAsync(projectId, "doc", [1f, 0f, 0f]);
        var otherId = await InsertDocumentAsync(otherProjectId, "other-doc", [1f, 0f, 0f]);

        var results = await _sut.SearchAsync([1f, 0f, 0f], null, null, null, null, null, limit: 10);

        results.Select(r => r.Id).ShouldContain(id);
        results.Select(r => r.Id).ShouldContain(otherId);
    }

    [Fact]
    public async Task SearchAsync_NoDocuments_ReturnsEmpty()
    {
        var projectId = await InsertProjectAsync();

        var results = await _sut.SearchAsync([1f, 0f, 0f], null, projectId, null, null, null, limit: 10);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_KindFilter_MatchesOnlyExactEquality()
    {
        var projectId = await InsertProjectAsync();
        var methodId = await InsertDocumentAsync(projectId, "m", [1f, 0f, 0f], kind: "method");
        await InsertDocumentAsync(projectId, "t", [1f, 0f, 0f], kind: "type");

        var results = await _sut.SearchAsync([1f, 0f, 0f], null, projectId, "method", null, null, limit: 10);

        results.Select(r => r.Id).ShouldBe([methodId]);
    }

    [Fact]
    public async Task SearchAsync_QualifiedNameEquals_MatchesExactlyOnly()
    {
        var projectId = await InsertProjectAsync();
        var exactId = await InsertDocumentAsync(projectId, "a", [1f, 0f, 0f], qualifiedName: "Foo.Bar.Baz");
        await InsertDocumentAsync(projectId, "b", [1f, 0f, 0f], qualifiedName: "Foo.Bar.BazQux");

        var results = await _sut.SearchAsync(
            [1f, 0f, 0f], null, projectId, null, QualifiedNameFilterOperator.Equals, "Foo.Bar.Baz", limit: 10);

        results.Select(r => r.Id).ShouldBe([exactId]);
    }

    [Fact]
    public async Task SearchAsync_QualifiedNameContainsWithWildcard_MatchesPattern()
    {
        var projectId = await InsertProjectAsync();
        var matchId = await InsertDocumentAsync(projectId, "a", [1f, 0f, 0f], qualifiedName: "Foo.Bar.Baz");
        await InsertDocumentAsync(projectId, "b", [1f, 0f, 0f], qualifiedName: "Foo.Qux.Baz");

        var results = await _sut.SearchAsync(
            [1f, 0f, 0f], null, projectId, null, QualifiedNameFilterOperator.Contains, "*Bar*", limit: 10);

        results.Select(r => r.Id).ShouldBe([matchId]);
    }

    [Fact]
    public async Task SearchAsync_QualifiedNameContainsWithoutWildcard_MatchesExactlyCaseInsensitive()
    {
        var projectId = await InsertProjectAsync();
        var matchId = await InsertDocumentAsync(projectId, "a", [1f, 0f, 0f], qualifiedName: "Foo.Bar.Baz");
        await InsertDocumentAsync(projectId, "b", [1f, 0f, 0f], qualifiedName: "Foo.Bar.BazQux");

        var results = await _sut.SearchAsync(
            [1f, 0f, 0f], null, projectId, null, QualifiedNameFilterOperator.Contains, "foo.bar.baz", limit: 10);

        results.Select(r => r.Id).ShouldBe([matchId]);
    }

    [Fact]
    public async Task SearchAsync_QualifiedNameNotContains_ExcludesMatchingDocuments()
    {
        var projectId = await InsertProjectAsync();
        await InsertDocumentAsync(projectId, "a", [1f, 0f, 0f], qualifiedName: "Foo.Bar.Baz");
        var keptId = await InsertDocumentAsync(projectId, "b", [1f, 0f, 0f], qualifiedName: "Foo.Qux.Baz");

        var results = await _sut.SearchAsync(
            [1f, 0f, 0f], null, projectId, null, QualifiedNameFilterOperator.NotContains, "*Bar*", limit: 10);

        results.Select(r => r.Id).ShouldBe([keptId]);
    }

    [Fact]
    public async Task SearchAsync_LimitLowerThanMatchCount_ReturnsOnlyTheClosestMatches()
    {
        var projectId = await InsertProjectAsync();
        var closestId = await InsertDocumentAsync(projectId, "closest", [1f, 0f, 0f]);
        await InsertDocumentAsync(projectId, "middle", [0.9f, 0.1f, 0f]);
        await InsertDocumentAsync(projectId, "farthest", [0f, 1f, 0f]);

        var results = (await _sut.SearchAsync([1f, 0f, 0f], null, projectId, null, null, null, limit: 1)).ToList();

        results.Select(r => r.Id).ShouldBe([closestId]);
    }

    [Fact]
    public async Task SearchAsync_ReturnsProjectGitUrlsAndConcatenatesRawUrlWithSourcePath()
    {
        const string gitUrl = "https://github.com/acme/widgets";
        const string gitRawUrl = "https://raw.githubusercontent.com/acme/widgets/main/";
        const string sourcePath = "src/Widgets/Widget.cs";
        var projectId = await InsertProjectAsync(gitUrl, gitRawUrl);
        await InsertDocumentAsync(projectId, "widget", [1f, 0f, 0f], sourcePath: sourcePath);

        var result = (await _sut.SearchAsync([1f, 0f, 0f], null, projectId, null, null, null, limit: 10)).Single();

        result.GitUrl.ShouldBe(new Uri(gitUrl));
        result.GitRawUrl.ShouldBe(new Uri(gitRawUrl + sourcePath));
    }

    [Fact]
    public async Task SearchAsync_ReturnsNullRawUrlWhenProjectRawUrlIsNull()
    {
        var projectId = await InsertProjectAsync(gitRawUrl: null);
        await InsertDocumentAsync(projectId, "widget", [1f, 0f, 0f], sourcePath: "src/Widgets/Widget.cs");

        var result = (await _sut.SearchAsync([1f, 0f, 0f], null, projectId, null, null, null, limit: 10)).Single();

        result.GitRawUrl.ShouldBeNull();
    }

    [Theory]
    [InlineData("https://raw.githubusercontent.com/acme/widgets/main", "src/Widgets/Widget.cs")]
    [InlineData("https://raw.githubusercontent.com/acme/widgets/main/", "/src/Widgets/Widget.cs")]
    public async Task SearchAsync_RawUrlAndSourcePath_AreSeparatedByExactlyOneSlash(string gitRawUrl, string sourcePath)
    {
        var projectId = await InsertProjectAsync(gitRawUrl: gitRawUrl);
        await InsertDocumentAsync(projectId, "widget", [1f, 0f, 0f], sourcePath: sourcePath);

        var result = (await _sut.SearchAsync([1f, 0f, 0f], null, projectId, null, null, null, limit: 10)).Single();

        result.GitRawUrl.ShouldBe(new Uri("https://raw.githubusercontent.com/acme/widgets/main/src/Widgets/Widget.cs"));
    }

    private async Task<long> InsertProjectAsync(string? gitUrl = null, string? gitRawUrl = null)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO public.projects (name, embedding_model, embedding_dimensions, git_url, git_raw_url)
            VALUES (@Name, 'test-model', 3, @GitUrl, @GitRawUrl)
            RETURNING id
            """,
            new { Name = $"proj-{Guid.NewGuid():N}", GitUrl = gitUrl, GitRawUrl = gitRawUrl });
    }

    private async Task<long> InsertDocumentAsync(
        long projectId,
        string symbolName,
        float[]? embedding,
        string kind = "method",
        string? qualifiedName = null,
        string? sourcePath = null)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO public.ciir_documents
                (project_id, ciir_id, schema_version, kind, language, symbol_name, symbol_qualified_name, source_path, embedding_text, content, embedding)
            VALUES
                (@ProjectId, @CiirId, '1.0', @Kind, 'csharp', @SymbolName, @QualifiedName, @SourcePath, @SymbolName, '{}'::jsonb, @Embedding)
            RETURNING id
            """,
            new
            {
                ProjectId = projectId,
                CiirId = $"sha256:{Guid.NewGuid():N}",
                Kind = kind,
                SymbolName = symbolName,
                QualifiedName = qualifiedName,
                SourcePath = sourcePath,
                Embedding = embedding is null ? null : new Vector(embedding),
            });
    }
}
