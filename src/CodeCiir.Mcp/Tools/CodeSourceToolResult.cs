namespace CodeCiir.Mcp.Tools;

/// <summary>File locators resolved from an id returned by <c>query_project_code</c>.</summary>
public sealed record CodeSourceToolResult(long DocumentId, string? SourceFile, Uri? GitRawUrl);
