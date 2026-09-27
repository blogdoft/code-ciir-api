namespace CodeCiir.Embeddings.Abstraction;

/// <summary>
/// Configuration bound from the "Embeddings" configuration section - connection settings for
/// whichever provider is selected via <see cref="Provider"/>, plus the single deployment-wide
/// <see cref="Model"/>/<see cref="Dimensions"/> used to embed a code-queries <c>question</c>.
/// <c>ciir_documents.embedding</c> has one fixed vector width for the whole installation (see
/// .specs/01-schema-discovery.md), and <c>projects.embedding_model</c>/<c>embedding_dimensions</c>
/// no longer exist (dropped 2026-09-27 - they were informational metadata, not a real per-row
/// capability) - question embedding always uses this single app-configured model. See
/// .specs/09-code-queries-filters.md.
/// </summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embeddings";

    /// <summary>
    /// Name of the provider to use, matched against <see cref="IEmbeddingProviderFactory.ProviderName"/>
    /// (e.g. "Ollama"). Matching is case-insensitive.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Base URL of the provider's HTTP API.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>API key/token used to authenticate with the provider, when required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Request timeout, in seconds, for HTTP-based providers.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Name of the embedding model used to embed every code-queries <c>question</c>.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Number of components in the vectors produced by <see cref="Model"/>.</summary>
    public int Dimensions { get; set; }
}
