using CodeCiir.Reranking.Abstraction;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeCiir.Reranking.Ollama;

/// <summary>
/// Scores candidates one at a time (pointwise) by calling Ollama's native <c>POST /api/generate</c>
/// endpoint with a structured-output JSON schema, bounded by a semaphore (<c>MaxConcurrency</c>)
/// so a large candidate pool doesn't open unbounded concurrent requests against Ollama.
/// </summary>
public sealed class OllamaReranker : IReranker
{
    // Asked of the model as an integer (structured-output JSON schemas can't express a bounded
    // decimal), then normalized to [0.0, 1.0] - a wide range gives RerankedCandidate.Score up to
    // 5 decimal digits of resolution (see .specs/10-reranking.md), even though no model reliably
    // means every one of those digits; it's still strictly better than the 1-digit resolution a
    // 0-10 scale gave, especially for downstream tie-breaking.
    private const int MaxScore = 100_000;

    private static readonly object ScoreFormat = new
    {
        type = "object",
        properties = new { score = new { type = "integer", minimum = 0, maximum = MaxScore } },
        required = new[] { "score" },
    };

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly SemaphoreSlim _concurrencyLimiter;

    public OllamaReranker(HttpClient httpClient, string model, int candidatePoolSize, int maxConcurrency)
    {
        _httpClient = httpClient;
        _model = model;
        CandidatePoolSize = candidatePoolSize;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency);
    }

    public string Provider => "Ollama";

    public int CandidatePoolSize { get; }

    public async Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query, IReadOnlyList<RerankCandidate> candidates, CancellationToken cancellationToken = default) =>
        await Task.WhenAll(candidates.Select(c => ScoreAsync(query, c, cancellationToken)));

    private static string BuildPrompt(string query, string candidateText) => $"""
        You are grading how relevant a piece of source code is to a search question.

        Question: {query}

        Code:
        {candidateText}

        Score how relevant this code is to answering the question, from 0 (not relevant at all) to
        {MaxScore} (exactly what the question is asking about). Respond with only the score.
        """;

    private async Task<RerankedCandidate> ScoreAsync(string query, RerankCandidate candidate, CancellationToken cancellationToken)
    {
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            var request = new OllamaGenerateRequest(_model, BuildPrompt(query, candidate.Text), false, ScoreFormat, new OllamaGenerateOptions(0));

            using var response = await _httpClient.PostAsJsonAsync("api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            var rawScore = body?.Response
                ?? throw new RerankingException("Ollama returned no response text while reranking.");

            var payload = JsonSerializer.Deserialize<OllamaScorePayload>(rawScore)
                ?? throw new RerankingException("Ollama's reranking response did not contain a score payload.");

            return new RerankedCandidate(candidate.Id, Math.Round(Math.Clamp(payload.Score, 0, MaxScore) / (double)MaxScore, 5));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException or JsonException)
        {
            throw new RerankingException($"Failed to rerank candidate {candidate.Id} using Ollama model '{_model}'.", ex);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private sealed record OllamaGenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("format")] object Format,
        [property: JsonPropertyName("options")] OllamaGenerateOptions Options);

    private sealed record OllamaGenerateOptions([property: JsonPropertyName("temperature")] double Temperature);

    private sealed record OllamaGenerateResponse([property: JsonPropertyName("response")] string? Response);

    private sealed record OllamaScorePayload([property: JsonPropertyName("score")] int Score);
}
