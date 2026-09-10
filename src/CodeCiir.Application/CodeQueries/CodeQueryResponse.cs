namespace CodeCiir.Application.CodeQueries;

/// <summary>
/// The result of a code-queries call: the semantic matches, plus up to 2 hops of their code
/// relationship graph - the requirement central to code-ciir-api, see
/// .specs/05-code-queries-relationship-graph.md.
/// </summary>
public sealed record CodeQueryResponse(IReadOnlyList<CodeQueryResult> Matches, CodeGraph Graph);
