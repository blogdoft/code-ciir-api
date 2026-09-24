using CodeCiir.Application.Projects;
using CodeCiir.Application.Tests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.Projects.ProjectsServiceTests;

public sealed class ListAsyncTests : BaseProjectsServiceTests
{
    [Fact]
    public async Task Should_ReturnRepositoryItemsWithDefaultPaging_When_NoArgumentIsGiven()
    {
        var expected = ProjectFaker.Create(2);
        ProjectsRepository.SearchAsync(null, 0, ProjectsService.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns((expected, 2L));

        var result = await Sut.ListAsync(null, null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new ProjectPage(expected, 0, ProjectsService.DefaultPageSize, 2, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnNameFilterEmptyFailure_When_NameFilterIsEmptyOrWhitespace(string nameFilter)
    {
        var result = await Sut.ListAsync(nameFilter, null, null);

        result.ShouldBeFailure(ProjectFailures.NameFilterEmpty());
    }

    [Fact]
    public async Task Should_ReturnNameFilterTooLongFailure_When_NameFilterExceedsMaximumLength()
    {
        var tooLong = new string('a', ProjectsService.MaxNameFilterLength + 1);

        var result = await Sut.ListAsync(tooLong, null, null);

        result.ShouldBeFailure(ProjectFailures.NameFilterTooLong(ProjectsService.MaxNameFilterLength));
    }

    [Fact]
    public async Task Should_AcceptNameFilter_When_ItHasExactlyTheMaximumLength()
    {
        var atMax = new string('a', ProjectsService.MaxNameFilterLength);
        var expected = ProjectFaker.Create(1);
        ProjectsRepository.SearchAsync(atMax, 0, ProjectsService.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns((expected, 1L));

        var result = await Sut.ListAsync(atMax, null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBe(expected);
    }

    [Fact]
    public async Task Should_ReturnPageInvalidFailure_When_PageIsNegative()
    {
        var result = await Sut.ListAsync(null, -1, null);

        result.ShouldBeFailure(ProjectFailures.PageInvalid());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ProjectsService.MaxPageSize + 1)]
    public async Task Should_ReturnPageSizeInvalidFailure_When_PageSizeIsOutOfRange(int pageSize)
    {
        var result = await Sut.ListAsync(null, null, pageSize);

        result.ShouldBeFailure(ProjectFailures.PageSizeInvalid(ProjectsService.MaxPageSize));
    }

    [Fact]
    public async Task Should_ForwardPageAndPageSizeToRepository_When_BothAreGiven()
    {
        ProjectsRepository.SearchAsync(null, 2, 5, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Project>(), 11L));

        var result = await Sut.ListAsync(null, 2, 5);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new ProjectPage([], 2, 5, 11, 3));
    }

    [Fact]
    public async Task Should_ReportZeroTotalPages_When_RepositoryHasNoProjects()
    {
        ProjectsRepository.SearchAsync(null, 0, ProjectsService.DefaultPageSize, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Project>(), 0L));

        var result = await Sut.ListAsync(null, null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalPages.ShouldBe(0);
    }
}
