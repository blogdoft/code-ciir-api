using CodeCiir.Reranking.Abstraction;
using System.Net.Http.Headers;

namespace CodeCiir.Reranking.OpenAI;

public sealed class OpenAIRerankerProviderFactory(IHttpClientFactory httpClientFactory) : IRerankerProviderFactory
{
    // Deliberate: this is only the fallback default - options.BaseUrl already makes the endpoint
    // configurable, e.g. to point at an OpenAI-compatible self-hosted gateway instead.
#pragma warning disable S1075
    private const string DefaultBaseUrl = "https://api.openai.com/v1/";
#pragma warning restore S1075

    public string ProviderName => "OpenAI";

    public IReranker Create(RerankingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException(
                $"'{RerankingOptions.SectionName}:{nameof(RerankingOptions.Model)}' must be set when using the OpenAI reranking provider.");
        }

        var httpClient = httpClientFactory.CreateClient(ServiceCollectionExtensions.HttpClientName);
        httpClient.BaseAddress = new Uri(options.BaseUrl ?? DefaultBaseUrl, UriKind.Absolute);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        // Optional, unlike the embeddings OpenAI provider: many OpenAI-compatible self-hosted
        // gateways (vLLM, LiteLLM, ...) don't require an API key at all.
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }

        return new OpenAIReranker(httpClient, options.Model, options.CandidatePoolSize, options.MaxConcurrency);
    }
}
