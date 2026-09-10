using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Projects;

/// <summary>
/// Domain failures for project lookups. <see cref="Failure.Code"/> is prefixed with the
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

    public static Failure ProjectNotFound(long projectId) => new(
        "404-project-not-found",
        $"No project exists with id {projectId}.");
}
