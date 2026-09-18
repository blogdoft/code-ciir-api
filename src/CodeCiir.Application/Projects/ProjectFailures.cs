using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Projects;

/// <summary>
/// Domain failures for project operations. <see cref="Failure.Code"/> is prefixed with the
/// intended HTTP status, translated by CodeCiir.Api.Problems.FailureResults.
/// </summary>
public static class ProjectFailures
{
    public static Failure NameFilterEmpty() => new(
        "400-name-filter-empty",
        "The 'name' query parameter must not be empty when provided.");

    public static Failure NameFilterTooLong(int maxLength) => new(
        "400-name-filter-too-long",
        $"The 'name' query parameter must not exceed {maxLength} characters.");

    public static Failure PageInvalid() => new(
        "400-page-invalid",
        "The 'page' query parameter must be zero or a positive integer.");

    public static Failure PageSizeInvalid(int maxPageSize) => new(
        "400-page-size-invalid",
        $"The 'page_size' query parameter must be between 1 and {maxPageSize}.");

    public static Failure ProjectNotFound(long projectId) => new(
        "404-project-not-found",
        $"No project exists with id {projectId}.");
}
