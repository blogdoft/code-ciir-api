namespace CodeCiir.Reranking.Abstraction;

/// <summary>
/// Configuration bound from the "Reranking" configuration section - connection settings for
/// whichever provider is selected via <see cref="Provider"/>. An empty/"None" <see cref="Provider"/>
/// is a valid, supported "disabled" configuration (resolves to a passthrough reranker), not a
/// configuration error - unlike <c>Embeddings:Provider</c>, which is required.
/// </summary>
public sealed class RerankingOptions
{
    public const string SectionName = "Reranking";

    /// <summary>
    /// Name of the provider to use, matched against <see cref="IRerankerProviderFactory.ProviderName"/>
    /// (e.g. "Ollama", "OpenAI"). Matching is case-insensitive. Empty or "None" disables reranking.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Name of the model the selected provider should use to score candidates.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Base URL of the provider's HTTP API. Providers may fall back to their own default when unset.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>API key/token used to authenticate with the provider, when required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Request timeout, in seconds, for HTTP-based providers.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>See <see cref="IReranker.CandidatePoolSize"/> - how many extra candidates to fetch before reranking.</summary>
    public int CandidatePoolSize { get; set; } = 25;

    /// <summary>Maximum number of candidates a provider may score concurrently (pointwise providers).</summary>
    public int MaxConcurrency { get; set; } = 6;
}
