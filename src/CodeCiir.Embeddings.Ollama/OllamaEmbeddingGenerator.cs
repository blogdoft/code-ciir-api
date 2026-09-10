using CodeCiir.Embeddings.Abstraction;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CodeCiir.Embeddings.Ollama;

/// <summary>
/// Generates embeddings by calling Ollama's native <c>POST /api/embed</c> endpoint.
/// </summary>
public sealed class OllamaEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;

    public OllamaEmbeddingGenerator(HttpClient httpClient, string model, int dimensions)
    {
        _httpClient = httpClient;
        Model = model;
        Dimensions = dimensions;
    }

    public string Provider => "Ollama";

    public string Model { get; }

    public int Dimensions { get; }

    public async Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync("api/embed", new OllamaEmbedRequest(Model, text), cancellationToken);

            response.EnsureSuccessStatusCode();

            var body = await response.Content
                .ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken);

            var embedding = body?.Embeddings?.FirstOrDefault()
                ?? throw new EmbeddingGenerationException("Ollama returned no embeddings in its response.");

            return new EmbeddingVector(embedding);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            throw new EmbeddingGenerationException($"Failed to generate an embedding using Ollama model '{Model}'.", ex);
        }
    }

    private sealed record OllamaEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed record OllamaEmbedResponse(
        [property: JsonPropertyName("embeddings")] List<float[]>? Embeddings);
}
