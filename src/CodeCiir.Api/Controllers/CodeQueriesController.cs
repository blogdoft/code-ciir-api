using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Api.Contracts;
using CodeCiir.Api.Problems;
using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Feedback;
using Microsoft.AspNetCore.Mvc;

namespace CodeCiir.Api.Controllers;

/// <summary>Operations for querying indexed source code using natural language.</summary>
// S6960: feedback is deliberately nested here rather than in its own controller - it is
// meaningless without a prior code-queries call on this same route, mirroring code-rag-api's
// own CodeQueriesController (.specs/06-code-query-feedback.md).
#pragma warning disable S6960
[ApiController]
[ApiExplorerSettings(GroupName = "code-queries")]
[Route("api/code-queries")]
public sealed class CodeQueriesController(ICodeQueryService codeQueryService, IFeedbackService feedbackService) : ControllerBase
{
    /// <summary>Query indexed code using natural language</summary>
    /// <remarks>
    /// Accepts a natural language question, converts it into a vector embedding using the single
    /// app-configured embedding model, and returns the code documents whose stored embeddings are
    /// most semantically similar to the question - optionally narrowed to one project and/or by
    /// structured filters, reordered by an optional reranking stage, and capped at <c>limit</c>.
    ///
    /// When no code document scores highly enough to be considered a match, the response is a 200
    /// OK with an empty array - this is not treated as an error. A 404 is returned only when
    /// <c>projectId</c> is provided and does not correspond to any project.
    ///
    /// <b>Why this endpoint has no page-based pagination:</b> results must always come back
    /// ordered from the single greatest match to the smallest. When a reranker is configured
    /// (server-side only, see <c>Reranking:Provider</c>), that ordering is decided by reranking
    /// the entire candidate pool - not by cosine similarity alone - before truncating to
    /// <c>limit</c>. Offset-based paging would rerank each page's candidates in isolation, which
    /// can hide a globally better match that happens to land on a later page, and cannot
    /// guarantee one consistent best-to-worst ordering across pages. Use <c>limit</c> to control
    /// how many top results you want; there is no <c>page</c>. See .specs/10-reranking.md.
    /// </remarks>
    /// <param name="request">
    /// The natural language question to search with, plus optional <c>projectId</c>,
    /// <c>minSimilarity</c>, <c>kind</c>, <c>qualifiedName</c> filters and <c>limit</c>.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">
    /// The code documents most semantically similar to the natural language question (`matches`,
    /// ordered by descending `rerankScore` when reranking is configured, otherwise by descending
    /// cosine `similarity`). Each match carries its own `relations` - every direct (1-hop)
    /// relation touching it, both directions, every relation type, always complete regardless of
    /// graph size (see .specs/12-match-relations.md). The response also includes up to 2 hops of
    /// the matches' code relationship graph (`graph`) - every relation type, both directions -
    /// when `projectId` was provided. `matches` is an empty array when no code document is found
    /// to be similar enough to the question, or matching the given filters; `graph` is then also
    /// empty. `graph` is always empty, and every match's `relations` is always an empty array,
    /// when `projectId` is omitted (see .specs/09-code-queries-filters.md).
    /// </response>
    /// <response code="400">
    /// The request body is missing the question field, has an empty/blank question, has an
    /// invalid `projectId`/`minSimilarity`/`kind`/`qualifiedName`, or is otherwise malformed.
    /// </response>
    /// <response code="404">
    /// `projectId` was provided but does not correspond to any project. This is the only
    /// condition under which this endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500; the response has no body.
    /// </response>
    [HttpPost]
    [ProducesResponseType<Contracts.CodeQueryResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> QueryAsync(
        [FromBody] CodeQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await codeQueryService.QueryAsync(
            request.Question,
            request.ProjectId,
            request.MinSimilarity,
            request.Kind,
            request.QualifiedName?.Operator,
            request.QualifiedName?.Value,
            request.Limit,
            cancellationToken);

        return result.Map(
            onSuccess: response => (IActionResult)Ok(Contracts.CodeQueryResponse.From(response)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    /// <summary>Submit feedback on a prior code query</summary>
    /// <remarks>
    /// Records whether a previous <c>POST .../code-queries</c> call's results were useful, so
    /// effectiveness can be measured later. Callable by a human (REST) or an AI agent (MCP, via
    /// the <c>submit_code_query_feedback</c> tool). The caller must always identify itself via
    /// the required <c>user</c> field - for MCP callers this is the calling agent/tool's own
    /// name (e.g. "claude code", "codex", "crewai", "hermes", "opencode"), never a guess or a
    /// default.
    ///
    /// There is no corresponding GET endpoint for feedback - effectiveness is computed directly
    /// against the database out of band. Accordingly, the 201 response below has no
    /// <c>Location</c> header.
    /// </remarks>
    /// <param name="request">
    /// The identifier of the project the original code-queries call was scoped to, the original
    /// question, whether it was useful, the similarity values it returned, and the identity of
    /// the caller submitting the feedback.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="201">
    /// The feedback was recorded. The response body echoes the persisted record, including its
    /// generated id and createdAt.
    /// </response>
    /// <response code="400">
    /// The request body is missing or malformed, projectId is missing or not a positive integer,
    /// question/useful/similarities/user is missing or invalid (empty, blank, exceeds its maximum
    /// length/count), or reason exceeds its maximum length.
    /// </response>
    /// <response code="404">
    /// No project exists with the given projectId. This is the only condition under which this
    /// endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500; the response has no body.
    /// </response>
    [HttpPost("feedback")]
    [ProducesResponseType<CodeQueryFeedbackResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitFeedbackAsync(
        [FromBody] CodeQueryFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var result = await feedbackService.SubmitAsync(
            request.ProjectId!.Value,
            request.Question,
            request.Useful,
            request.Similarities,
            request.Reason,
            request.User,
            cancellationToken);

        return result.Map(
            onSuccess: feedback => (IActionResult)StatusCode(StatusCodes.Status201Created, CodeQueryFeedbackResponse.From(feedback)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }
}
#pragma warning restore S6960
