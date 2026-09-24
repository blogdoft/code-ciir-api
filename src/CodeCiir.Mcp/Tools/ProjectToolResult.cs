namespace CodeCiir.Mcp.Tools;

/// <summary>MCP mirror of ProjectResponse - see CodeCiir.Api.Contracts.ProjectResponse.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectToolResult(
    Guid Id,
    string Name,
    string EmbeddingModel,
    int EmbeddingDimensions,
    DateTime CreatedAt,
    DateTime UpdatedAt);
#pragma warning restore SA1313
