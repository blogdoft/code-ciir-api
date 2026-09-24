namespace CodeCiir.Mcp.Tools;

/// <summary>MCP mirror of CodeQueryFeedbackResponse.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryFeedbackToolResult(
    long Id,
    Guid ProjectId,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string User,
    DateTime CreatedAt);
#pragma warning restore SA1313
