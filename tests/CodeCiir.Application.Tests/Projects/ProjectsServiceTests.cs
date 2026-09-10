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
        _projectsRepository.SearchAsync(null, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.ListAsync(null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListAsync_EmptyOrWhitespaceFilter_ReturnsNameFilterEmpty(string nameFilter)
    {
        var result = await _sut.ListAsync(nameFilter);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-name-filter-empty");
    }

    [Fact]
    public async Task ListAsync_FilterTooLong_ReturnsNameFilterTooLong()
    {
        var tooLong = new string('a', ProjectsService.MaxNameFilterLength + 1);

        var result = await _sut.ListAsync(tooLong);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-name-filter-too-long");
    }

    [Fact]
    public async Task ListAsync_FilterAtMaxLength_IsAccepted()
    {
        var atMax = new string('a', ProjectsService.MaxNameFilterLength);
        _projectsRepository.SearchAsync(atMax, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.ListAsync(atMax);

        result.IsSuccess.ShouldBeTrue();
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

    private Project CreateProject() => new(
        _faker.Random.Long(1, 1000),
        _faker.Commerce.ProductName(),
        "bge-m3",
        1024,
        _faker.Date.PastOffset().UtcDateTime,
        _faker.Date.RecentOffset().UtcDateTime);
}
