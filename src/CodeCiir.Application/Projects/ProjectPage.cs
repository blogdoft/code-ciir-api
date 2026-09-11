namespace CodeCiir.Application.Projects;

/// <summary>One page of a paginated <see cref="Project"/> search, plus the metadata needed to fetch the next one.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectPage(
    IReadOnlyList<Project> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);
#pragma warning restore SA1313
