using Bogus;
using CodeCiir.Application.Projects;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.Projects;

public sealed class ProjectsServiceTests
{
    private readonly IProjectsRepository _projectsRepository = Substitute.For<IProjectsRepository>();
    private readonly ProjectsService _sut;
    private readonly Faker _faker = new();

    public ProjectsServiceTests()
    {
        _sut = new ProjectsService(_projectsRepository);
    }

    [Fact]
    public async Task ListAsync_NoFilter_ReturnsWhateverRepositoryReturns()
    {
        var expected = new[] { CreateProject(), CreateProject() };
        _projectsRepository.SearchAsync(null, 0, ProjectsService.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns((expected, 2L));

        var result = await _sut.ListAsync(null, null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBe(expected);
        result.Value.Page.ShouldBe(0);
        result.Value.PageSize.ShouldBe(ProjectsService.DefaultPageSize);
        result.Value.TotalCount.ShouldBe(2);
        result.Value.TotalPages.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListAsync_EmptyOrWhitespaceFilter_ReturnsNameFilterEmpty(string nameFilter)
    {
        var result = await _sut.ListAsync(nameFilter, null, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-name-filter-empty");
    }

    [Fact]
    public async Task ListAsync_FilterTooLong_ReturnsNameFilterTooLong()
    {
        var tooLong = new string('a', ProjectsService.MaxNameFilterLength + 1);

        var result = await _sut.ListAsync(tooLong, null, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-name-filter-too-long");
    }

    [Fact]
    public async Task ListAsync_FilterAtMaxLength_IsAccepted()
    {
        var atMax = new string('a', ProjectsService.MaxNameFilterLength);
        _projectsRepository.SearchAsync(atMax, 0, ProjectsService.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Project>(), 0L));

        var result = await _sut.ListAsync(atMax, null, null);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAsync_NegativePage_ReturnsPageInvalid()
    {
        var result = await _sut.ListAsync(null, -1, null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-page-invalid");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ProjectsService.MaxPageSize + 1)]
    public async Task ListAsync_PageSizeOutOfRange_ReturnsPageSizeInvalid(int pageSize)
    {
        var result = await _sut.ListAsync(null, null, pageSize);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-page-size-invalid");
    }

    [Fact]
    public async Task ListAsync_ExplicitPageAndPageSize_PassesThroughToRepository()
    {
        _projectsRepository.SearchAsync(null, 2, 5, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Project>(), 11L));

        var result = await _sut.ListAsync(null, 2, 5);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(5);
        result.Value.TotalCount.ShouldBe(11);
        result.Value.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task GetAsync_ExistingProject_ReturnsIt()
    {
        var project = CreateProject();
        _projectsRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var result = await _sut.GetAsync(project.Id);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(project);
    }

    [Fact]
    public async Task GetAsync_MissingProject_ReturnsProjectNotFound()
    {
        _projectsRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.GetAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("404-project-not-found");
    }

    [Fact]
    public async Task CreateAsync_ValidFields_InsertsAndReturnsProject()
    {
        var created = CreateProject();
        _projectsRepository.ExistsByNameAsync(created.Name, null, Arg.Any<CancellationToken>()).Returns(false);
        _projectsRepository
            .InsertAsync(
                created.Name,
                created.EmbeddingModel,
                created.EmbeddingDimensions,
                new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
                new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"),
                Arg.Any<CancellationToken>())
            .Returns(created);

        var result = await _sut.CreateAsync(
            created.Name,
            created.EmbeddingModel,
            created.EmbeddingDimensions,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(created);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_MissingName_ReturnsNameRequired(string? name)
    {
        var result = await _sut.CreateAsync(
            name,
            "bge-m3",
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-name-required");
    }

    [Fact]
    public async Task CreateAsync_NameTooLong_ReturnsNameTooLong()
    {
        var tooLong = new string('a', ProjectsService.MaxNameLength + 1);

        var result = await _sut.CreateAsync(
            tooLong,
            "bge-m3",
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-name-too-long");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_MissingEmbeddingModel_ReturnsEmbeddingModelRequired(string? embeddingModel)
    {
        var result = await _sut.CreateAsync(
            "proj",
            embeddingModel,
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-embedding-model-required");
    }

    [Fact]
    public async Task CreateAsync_MissingEmbeddingDimensions_ReturnsEmbeddingDimensionsRequired()
    {
        var result = await _sut.CreateAsync(
            "proj",
            "bge-m3",
            null,
            null,
            null);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-embedding-dimensions-required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_NonPositiveEmbeddingDimensions_ReturnsEmbeddingDimensionsInvalid(int embeddingDimensions)
    {
        var result = await _sut.CreateAsync(
            "proj",
            "bge-m3",
            embeddingDimensions,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-embedding-dimensions-invalid");
    }

    [Fact]
    public async Task CreateAsync_NameAlreadyUsed_ReturnsNameConflict()
    {
        _projectsRepository.ExistsByNameAsync("proj", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.CreateAsync(
            "proj",
            "bge-m3",
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("409-name-conflict");
    }

    [Fact]
    public async Task UpdateAsync_ExistingProject_UpdatesAndReturnsIt()
    {
        var existing = CreateProject();
        var updated = existing with { Name = "renamed" };
        _projectsRepository.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _projectsRepository.ExistsByNameAsync("renamed", existing.Id, Arg.Any<CancellationToken>()).Returns(false);
        _projectsRepository
            .UpdateAsync(
                existing.Id,
                "renamed",
                existing.EmbeddingModel,
                existing.EmbeddingDimensions,
                new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
                new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"),
                Arg.Any<CancellationToken>())
            .Returns(updated);

        var result = await _sut.UpdateAsync(
            existing.Id,
            "renamed",
            existing.EmbeddingModel,
            existing.EmbeddingDimensions,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(updated);
    }

    [Fact]
    public async Task UpdateAsync_MissingProject_ReturnsProjectNotFound()
    {
        _projectsRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var result = await _sut.UpdateAsync(
            999,
            "proj",
            "bge-m3",
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("404-project-not-found");
    }

    [Fact]
    public async Task UpdateAsync_NameUsedByAnotherProject_ReturnsNameConflict()
    {
        var existing = CreateProject();
        _projectsRepository.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _projectsRepository.ExistsByNameAsync("taken", existing.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.UpdateAsync(
            existing.Id,
            "taken",
            existing.EmbeddingModel,
            existing.EmbeddingDimensions,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("409-name-conflict");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UpdateAsync_MissingName_ReturnsNameRequired(string? name)
    {
        var result = await _sut.UpdateAsync(
            1,
            name,
            "bge-m3",
            1024,
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
            new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"));

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-name-required");
    }

    [Fact]
    public async Task DeleteAsync_ExistingProject_ReturnsSuccess()
    {
        _projectsRepository.DeleteAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.DeleteAsync(1);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_MissingProject_ReturnsProjectNotFound()
    {
        _projectsRepository.DeleteAsync(999, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.DeleteAsync(999);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("404-project-not-found");
    }

    private Project CreateProject() => new(
        _faker.Random.Long(1, 1000),
        _faker.Commerce.ProductName(),
        "bge-m3",
        1024,
        new Uri("https://forgejo.home.arpa/sauron/code-ciir-api"),
        new Uri("https://forgejo.home.arpa/sauron/code-ciir-api/raw/branch/main/"),
        _faker.Date.PastOffset().UtcDateTime,
        _faker.Date.RecentOffset().UtcDateTime);
}
