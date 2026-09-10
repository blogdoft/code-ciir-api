namespace CodeCiir.Api.Contracts;

/// <summary>
/// Response of a code-queries call: the semantic matches, plus up to 2 hops of their code
/// relationship graph (all relation types, both directions) - the requirement central to
/// code-ciir-api. Deliberately a different shape than code-rag-api's plain array response - see
/// .specs/05-code-queries-relationship-graph.md.
/// </summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record CodeQueryResponse(
    IReadOnlyList<CodeQueryResultResponse> Matches,
    CodeQueryGraphResponse Graph);
#pragma warning restore SA1313
