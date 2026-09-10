using CodeCiir.Embeddings.Abstraction;

namespace CodeCiir.Embeddings.Ollama;

public sealed class OllamaEmbeddingProviderFactory(IHttpClientFactory httpClientFactory) : IEmbeddingProviderFactory
{
    public string ProviderName => "Ollama";

    public IEmbeddingGenerator Create(EmbeddingOptions options, string model, int dimensions)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                $"'{EmbeddingOptions.SectionName}:{nameof(EmbeddingOptions.BaseUrl)}' must be set when using the Ollama embedding provider.");
        }

        var httpClient = httpClientFactory.CreateClient(ServiceCollectionExtensions.HttpClientName);
        httpClient.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        return new OllamaEmbeddingGenerator(httpClient, model, dimensions);
    }
}
