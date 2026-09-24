using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.Projects;
using CodeCiir.Embeddings.Abstraction;
using CodeCiir.Reranking.Abstraction;
using Microsoft.Extensions.Options;

namespace CodeCiir.Application.CodeQueries;

public sealed class CodeQueryService(
    IProjectsRepository projectsRepository,
    ICodeDocumentsRepository codeDocumentsRepository,
    IRelationshipGraphRepository relationshipGraphRepository,
    EmbeddingGeneratorResolver embeddingResolver,
    IOptions<EmbeddingOptions> embeddingOptions,
    IReranker reranker) : ICodeQueryService
{
    public const int MaxQuestionLength = 1000;
    public const int DefaultLimit = 10;
    public const int MaxResultLimit = 50;
    public const int MaxFilterValueLength = 200;

    /// <summary>
    /// Defensive ceiling on how many candidates a reranker's <see cref="IReranker.CandidatePoolSize"/>
    /// can pull from the vector search, independent of <see cref="MaxResultLimit"/> (which only
    /// bounds the caller-supplied <c>limit</c>). Guards against a misconfigured, very large pool
    /// size turning every query into an expensive full-table-ish scan.
    /// </summary>
    public const int MaxCandidatePoolSize = 200;

    /// <summary>
    /// Fixed by requirement (not configurable): the graph is expanded exactly 2 hops from each
    /// match. See .specs/05-code-queries-relationship-graph.md.
    /// </summary>
    public const int MaxGraphDepth = 2;

    /// <summary>
    /// Safety cap on total distinct graph nodes per query, generous relative to the worst-case
    /// node degree observed in code3rag today (max out-degree 48, avg 8.53 - see
    /// .specs/01-schema-discovery.md). Guards against a future hub node causing runaway fan-out.
    /// </summary>
    public const int MaxGraphNodes = 100;

    public async Task<Result<CodeQueryResponse>> QueryAsync(
        string? question,
        Guid? projectId = null,
        double? minSimilarity = null,
        string? kind = null,
        QualifiedNameFilterOperator? qualifiedNameOperator = null,
        string? qualifiedNameValue = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return CodeQueryFailures.QuestionRequired();
        }

        if (question.Length > MaxQuestionLength)
        {
            return CodeQueryFailures.QuestionTooLong(MaxQuestionLength);
        }

        if (minSimilarity is < 0.0 or > 1.0)
        {
            return CodeQueryFailures.MinSimilarityOutOfRange();
        }

        var kindValidationFailure = ValidateKind(kind);
        if (kindValidationFailure is not null)
        {
            return kindValidationFailure;
        }

        var qualifiedNameValidationFailure = ValidateFilterValue(
            qualifiedNameOperator,
            qualifiedNameValue,
            CodeQueryFailures.QualifiedNameFilterValueRequired(),
            CodeQueryFailures.QualifiedNameFilterValueTooLong);
        if (qualifiedNameValidationFailure is not null)
        {
            return qualifiedNameValidationFailure;
        }

        long? internalProjectId = null;
        if (projectId is not null)
        {
            var project = await projectsRepository.GetByPublicIdAsync(projectId.Value, cancellationToken);
            if (project is null)
            {
                return ProjectFailures.ProjectNotFound(projectId.Value);
            }

            internalProjectId = project.Id;
        }

        // The question is always embedded with the single app-configured model/dimensions
        // (EmbeddingOptions.Model/Dimensions), never a per-project value - see EmbeddingOptions
        // and .specs/09-code-queries-filters.md for why.
        var options = embeddingOptions.Value;
        var generator = embeddingResolver.Resolve(options.Model, options.Dimensions);
        var questionEmbedding = await generator.GenerateAsync(question, cancellationToken);

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxResultLimit);

        // Widen the vector search beyond effectiveLimit when the reranker wants a bigger pool to
        // choose from (0 when reranking is disabled, collapsing this back to effectiveLimit) -
        // capped defensively at MaxCandidatePoolSize regardless of what the reranker requests.
        var searchLimit = Math.Min(Math.Max(effectiveLimit, reranker.CandidatePoolSize), MaxCandidatePoolSize);

        var candidates = (await codeDocumentsRepository.SearchAsync(
            questionEmbedding.Values,
            minSimilarity,
            internalProjectId,
            kind,
            qualifiedNameOperator,
            qualifiedNameValue,
            searchLimit,
            cancellationToken)).ToList();

        // Reranking always runs, even when disabled: a NoOpReranker returns every candidate
        // unchanged with a null score, so there is no branching here on whether reranking is
        // actually configured.
        var rerankedScores = await reranker.RerankAsync(
            question, candidates.ConvertAll(c => new RerankCandidate(c.Id, c.EmbeddingText ?? string.Empty)), cancellationToken);
        var scoresById = rerankedScores.ToDictionary(r => r.Id, r => r.Score);

        // Stable sort: ties (including every candidate when reranking is disabled, since every
        // score is then null) keep the original cosine-similarity order. This - not any ordering
        // guarantee from the reranker itself - is what makes the endpoint's greatest-to-least
        // guarantee hold regardless of whether reranking is configured. Global reordering over
        // every candidate (not per-page) is exactly why this endpoint has no page-based
        // pagination - see CodeQueriesController.QueryAsync's remarks.
        var results = candidates
            .Select(c => c with { RerankScore = scoresById[c.Id] })
            .OrderByDescending(c => c.RerankScore ?? double.NegativeInfinity)
            .Take(effectiveLimit)
            .ToList();

        // ciir_relations rows are themselves scoped by project_id (see RelationshipGraphRepository),
        // so a single GetGraphAsync/GetDirectRelationsAsync call only makes sense against one
        // project. When projectId is omitted the search can return matches from several projects
        // at once, and expanding each one's relationship graph and merging them was never asked
        // for - matches-only (empty graph, no per-match relations) is the defensible behavior
        // here. See .specs/09-code-queries-filters.md.
        var rootIds = results.Select(r => r.Id).ToList();
        var graph = rootIds.Count > 0 && internalProjectId is not null
            ? await relationshipGraphRepository.GetGraphAsync(internalProjectId.Value, rootIds, MaxGraphDepth, MaxGraphNodes, cancellationToken)
            : CodeGraph.Empty;

        // Each match's own complete 1-hop relations, independent of MaxGraphNodes truncation -
        // see MatchRelation. See .specs/12-match-relations.md.
        var relationsById = rootIds.Count > 0 && internalProjectId is not null
            ? await relationshipGraphRepository.GetDirectRelationsAsync(internalProjectId.Value, rootIds, cancellationToken)
            : new Dictionary<long, IReadOnlyList<MatchRelation>>();
        results = results
            .Select(r => r with { Relations = relationsById.TryGetValue(r.Id, out var relations) ? relations : [] })
            .ToList();

        return Result<CodeQueryResponse>.FromSuccess(new CodeQueryResponse(results, graph));
    }

    // Value is only required/validated when the caller actually sets the operator - null operator
    // means "no filter", regardless of what value was passed alongside it.
    private static Failure? ValidateFilterValue<TOperator>(
        TOperator? filterOperator,
        string? value,
        Failure valueRequiredFailure,
        Func<int, Failure> valueTooLongFailure)
        where TOperator : struct
    {
        if (filterOperator is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return valueRequiredFailure;
        }

        return value.Length > MaxFilterValueLength ? valueTooLongFailure(MaxFilterValueLength) : null;
    }

    private static Failure? ValidateKind(string? kind)
    {
        if (kind is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(kind))
        {
            return CodeQueryFailures.KindFilterValueRequired();
        }

        return kind.Length > MaxFilterValueLength ? CodeQueryFailures.KindFilterValueTooLong(MaxFilterValueLength) : null;
    }
}
