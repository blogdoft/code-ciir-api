namespace CodeCiir.Api.Contracts;

/// <summary>A single page of a paginated project search, plus the metadata needed to fetch the next one.</summary>
/// <param name="Items">The projects on this page.</param>
/// <param name="Page">The zero-based page number this response corresponds to.</param>
/// <param name="PageSize">The maximum number of items per page.</param>
/// <param name="TotalCount">The total number of projects matching the filter, across every page.</param>
/// <param name="TotalPages">The total number of pages available for the filter, given <c>page_size</c>.</param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectListResponse(
    IReadOnlyList<ProjectResponse> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);
#pragma warning restore SA1313
