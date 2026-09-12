using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Feedback;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CodeCiir.Mcp.Tools;

[McpServerToolType]
public sealed class CodeQueryTools(
    ICodeQueryService codeQueryService,
    IFeedbackService feedbackService,
    ICodeDocumentSourceService codeDocumentSourceService)
{
    [McpServerTool(Name = "query_project_code")]
    [Description(
        "Searches indexed code (code3rag) using a natural language question, via vector " +
        "similarity, optionally narrowed to one project and/or by structured filters. Returns " +
        "`matches` (the most semantically similar code entities), each carrying its own " +
        "`relations` - every direct (1-hop) relation touching that match, both directions, every " +
        "relation type, always complete regardless of graph size - PLUS `graph` - up to 2 hops of " +
        "the code relationship graph expanded from those matches, every relation type, both " +
        "directions (who a match calls/reads/etc., and who calls/reads/etc. it back); `graph` and " +
        "every match's `relations` are only populated when projectId is given. Use " +
        "`graph.edges`/`graph.nodes` to explore structural context beyond the matched text itself " +
        "- e.g. a match's callers, or what it depends on. Follow up with " +
        "get_code_source using a selected `matches[].id` when you need the file path or raw Git URL. " +
        "submit_code_query_feedback reporting whether the results were useful.")]
    public async Task<CodeQueryToolResult> QueryProjectCodeAsync(
        [Description("Natural language question describing the code being looked for.")] string question,
        [Description("Id of the project to search, from list_projects. Omit to search across every project (graph expansion is then skipped).")] long? projectId = null,
        [Description("Minimum cosine similarity (0.0-1.0) a match must have to be included.")] double? minSimilarity = null,
        [Description("Exact 'kind' value to filter matches by (equality only).")] string? kind = null,
        [Description("Comparison operator for qualifiedNameValue.")] QualifiedNameFilterOperator? qualifiedNameOperator = null,
        [Description("Value to compare the member's fully qualified name against. '*' acts as a wildcard for Contains/NotContains.")] string? qualifiedNameValue = null,
        [Description("Max number of matches to return (default 10, capped at 50). No page-based pagination: results are always globally ordered greatest-to-smallest match, including when reranking is configured server-side.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var result = await codeQueryService.QueryAsync(
            question, projectId, minSimilarity, kind, qualifiedNameOperator, qualifiedNameValue, limit, cancellationToken);

        return result.Map(
            onSuccess: ToResult,
            onFailure: failure => throw new McpException(failure.Message));
    }

    [McpServerTool(Name = "get_code_source")]
    [Description(
        "Use this after query_project_code, passing a selected `matches[].id`. Returns the " +
        "source-file path from the project root and, when configured, the direct raw Git URL " +
        "for that same file. It returns file references only; it does not download file contents.")]
    public async Task<CodeSourceToolResult> GetCodeSourceAsync(
        [Description("Exact id of a selected item in query_project_code's `matches`; this is not a projectId or graph node/edge id.")] long documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await codeDocumentSourceService.GetAsync(documentId, cancellationToken);

        return result.Map(
            onSuccess: source => new CodeSourceToolResult(source.DocumentId, source.SourceFile, source.GitRawUrl),
            onFailure: failure => throw new McpException(failure.Message));
    }

    [McpServerTool(Name = "submit_code_query_feedback")]
    [Description(
        "Reports whether a prior query_project_code call's results were useful. Always call " +
        "this after query_project_code once you know whether the results helped. Echo back the " +
        "exact `similarity` values (not depths/relation types) from the matches you received.")]
    public async Task<CodeQueryFeedbackToolResult> SubmitCodeQueryFeedbackAsync(
        [Description("Id of the project the original query_project_code call was scoped to.")] long projectId,
        [Description("The exact question originally sent to query_project_code.")] string question,
        [Description("Whether the returned matches/graph were useful to accomplish the task.")] bool useful,
        [Description("The exact `similarity` values from the original matches, in the order received. Empty array if there were no matches.")] IReadOnlyList<double> similarities,
        [Description(
            "Identity of the calling agent/tool itself (e.g. \"claude code\", \"codex\", \"crewai\", " +
            "\"hermes\", \"opencode\") - never omitted or guessed.")]
        string user,
        [Description("Optional free-text explanation of why the results were not useful.")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var result = await feedbackService.SubmitAsync(projectId, question, useful, similarities, reason, user, cancellationToken);

        return result.Map(
            onSuccess: ToFeedbackResult,
            onFailure: failure => throw new McpException(failure.Message));
    }

    private static CodeQueryToolResult ToResult(CodeQueryResponse response) => new(
        response.Matches.Select(ToMatchToolResult).ToList(),
        new CodeQueryGraphToolResult(
            response.Graph.Nodes.Select(n => new CodeQueryGraphNodeToolResult(
                n.Id, n.Kind, n.SymbolContainer, n.SymbolName, n.SymbolQualifiedName, n.SymbolCanonicalName, n.SourceFile, n.Depth)).ToList(),
            response.Graph.Edges.Select(e => new CodeQueryGraphEdgeToolResult(
                e.FromId, e.ToId, e.RelationType, e.TargetSymbol, e.ResolutionOrigin, e.Depth)).ToList(),
            response.Graph.Truncated));

    private static CodeQueryMatchToolResult ToMatchToolResult(CodeQueryResult m) => new(
        m.Id,
        m.Kind,
        m.SymbolContainer,
        m.SymbolName,
        m.SymbolQualifiedName,
        m.SymbolCanonicalName,
        m.SourceFile,
        m.EmbeddingText,
        m.Similarity,
        m.RerankScore,
        (m.Relations ?? []).Select(ToRelationToolResult).ToList());

    private static CodeQueryRelationToolResult ToRelationToolResult(MatchRelation r) => new(
        r.FromId, r.ToId, r.RelationType, r.TargetSymbol, r.ResolutionOrigin);

    private static CodeQueryFeedbackToolResult ToFeedbackResult(FeedbackResult result) => new(
        result.Id, result.ProjectId, result.Question, result.Useful, result.Similarities, result.Reason, result.User, result.CreatedAt);
}
