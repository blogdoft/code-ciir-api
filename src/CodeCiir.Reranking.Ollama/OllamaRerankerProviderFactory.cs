using CodeCiir.Reranking.Abstraction;

namespace CodeCiir.Reranking.Ollama;

public sealed class OllamaRerankerProviderFactory(IHttpClientFactory httpClientFactory) : IRerankerProviderFactory
{
    public string ProviderName => "Ollama";

    public IReranker Create(RerankingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                $"'{RerankingOptions.SectionName}:{nameof(RerankingOptions.BaseUrl)}' must be set when using the Ollama reranking provider.");
        }

        var httpClient = httpClientFactory.CreateClient(ServiceCollectionExtensions.HttpClientName);
        httpClient.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        return new OllamaReranker(httpClient, options.Model, options.CandidatePoolSize, options.MaxConcurrency);
    }
}
