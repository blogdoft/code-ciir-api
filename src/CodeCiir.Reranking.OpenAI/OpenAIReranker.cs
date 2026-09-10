using CodeCiir.Reranking.Abstraction;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeCiir.Reranking.OpenAI;

/// <summary>
/// Scores candidates one at a time (pointwise) by calling the OpenAI Chat Completions wire
/// format (<c>POST {BaseUrl}/chat/completions</c>) with a strict JSON Schema response format.
/// Works against the real OpenAI API or any OpenAI-compatible self-hosted gateway (vLLM,
/// LiteLLM, Ollama's own <c>/v1</c> endpoint, ...) that implements the same protocol - see
/// <see cref="OpenAIRerankerProviderFactory"/> for the configurable base URL. Bounded by a
/// semaphore (<c>MaxConcurrency</c>) so a large candidate pool doesn't open unbounded concurrent
/// requests against the provider.
/// </summary>
public sealed class OpenAIReranker : IReranker
{
    // Asked of the model as an integer (structured-output JSON schemas can't express a bounded
    // decimal), then normalized to [0.0, 1.0] - a wide range gives RerankedCandidate.Score up to
    // 5 decimal digits of resolution (see .specs/10-reranking.md), even though no model reliably
    // means every one of those digits; it's still strictly better than the 1-digit resolution a
    // 0-10 scale gave, especially for downstream tie-breaking.
    private const int MaxScore = 100_000;

    private static readonly string SystemPrompt =
        "You are a precise relevance grader. Given a natural language question and a piece of " +
        "source code, respond with a JSON object containing only a 'score' field: an integer " +
        $"from 0 (not relevant at all) to {MaxScore} (exactly what the question is asking about).";

    private static readonly object ResponseFormat = new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "relevance_score",
            strict = true,
            schema = new
            {
                type = "object",
                properties = new { score = new { type = "integer", minimum = 0, maximum = MaxScore } },
                required = new[] { "score" },
                additionalProperties = false,
            },
        },
    };

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly SemaphoreSlim _concurrencyLimiter;

    public OpenAIReranker(HttpClient httpClient, string model, int candidatePoolSize, int maxConcurrency)
    {
        _httpClient = httpClient;
        _model = model;
        CandidatePoolSize = candidatePoolSize;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency);
    }

    public string Provider => "OpenAI";

    public int CandidatePoolSize { get; }

    public async Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query, IReadOnlyList<RerankCandidate> candidates, CancellationToken cancellationToken = default) =>
        await Task.WhenAll(candidates.Select(c => ScoreAsync(query, c, cancellationToken)));

    private static string BuildUserMessage(string query, string candidateText) => $"""
        Question: {query}

        Code:
        {candidateText}
        """;

    private async Task<RerankedCandidate> ScoreAsync(string query, RerankCandidate candidate, CancellationToken cancellationToken)
    {
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            var request = new ChatCompletionRequest(
                _model,
                0,
                [
                    new ChatMessage("system", SystemPrompt),
                    new ChatMessage("user", BuildUserMessage(query, candidate.Text)),
                ],
                ResponseFormat);

            using var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken);
            var rawScore = body?.Choices?.FirstOrDefault()?.Message?.Content
                ?? throw new RerankingException("OpenAI-compatible provider returned no message content while reranking.");

            var payload = JsonSerializer.Deserialize<ScorePayload>(rawScore)
                ?? throw new RerankingException("OpenAI-compatible provider's reranking response did not contain a score payload.");

            return new RerankedCandidate(candidate.Id, Math.Round(Math.Clamp(payload.Score, 0, MaxScore) / (double)MaxScore, 5));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException or JsonException)
        {
            throw new RerankingException($"Failed to rerank candidate {candidate.Id} using OpenAI-compatible model '{_model}'.", ex);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("response_format")] object Format);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse([property: JsonPropertyName("choices")] List<ChatCompletionChoice>? Choices);

    private sealed record ChatCompletionChoice([property: JsonPropertyName("message")] ChatCompletionMessage? Message);

    private sealed record ChatCompletionMessage([property: JsonPropertyName("content")] string? Content);

    private sealed record ScorePayload([property: JsonPropertyName("score")] int Score);
}
