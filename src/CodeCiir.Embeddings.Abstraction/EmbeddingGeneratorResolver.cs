using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CodeCiir.Embeddings.Abstraction;

/// <summary>
/// Picks the <see cref="IEmbeddingProviderFactory"/> matching the configured provider and caches
/// one <see cref="IEmbeddingGenerator"/> per distinct (model, dimensions) pair it is asked to
/// resolve. Callers always resolve with the single app-configured <see cref="EmbeddingOptions.Model"/>/
/// <see cref="EmbeddingOptions.Dimensions"/> (see .specs/09-code-queries-filters.md), so in
/// practice this only ever caches one generator - the (model, dimensions) parameter stays generic
/// so a caller isn't forced to reach into <see cref="EmbeddingOptions"/> itself.
/// </summary>
public sealed class EmbeddingGeneratorResolver
{
    private readonly IReadOnlyDictionary<string, IEmbeddingProviderFactory> _factories;
    private readonly EmbeddingOptions _options;
    private readonly ConcurrentDictionary<(string Model, int Dimensions), IEmbeddingGenerator> _cache = new();

    public EmbeddingGeneratorResolver(
        IEnumerable<IEmbeddingProviderFactory> factories,
        IOptions<EmbeddingOptions> options)
    {
        _factories = factories.ToDictionary(f => f.ProviderName, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
    }

    /// <summary>
    /// Fails fast on a missing/unknown provider name, or a missing/invalid <c>Model</c>/
    /// <c>Dimensions</c> - call once at startup so an operator error (typo in
    /// <c>Embeddings:Provider</c>, or a forgotten <c>Embeddings:Model</c>/<c>Embeddings:Dimensions</c>)
    /// surfaces immediately instead of on the first real request.
    /// </summary>
    public void ValidateProviderConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Provider))
        {
            throw new InvalidOperationException(
                $"No embedding provider configured. Set '{EmbeddingOptions.SectionName}:Provider' to one of: " +
                string.Join(", ", _factories.Keys));
        }

        if (!_factories.ContainsKey(_options.Provider))
        {
            throw new InvalidOperationException(
                $"Unknown embedding provider '{_options.Provider}'. Available providers: " +
                string.Join(", ", _factories.Keys));
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException(
                $"No embedding model configured. Set '{EmbeddingOptions.SectionName}:Model'.");
        }

        if (_options.Dimensions <= 0)
        {
            throw new InvalidOperationException(
                $"'{EmbeddingOptions.SectionName}:Dimensions' must be a positive integer.");
        }
    }

    /// <summary>Resolves (and caches) the embedding generator for the given model/dimensions pair.</summary>
    public IEmbeddingGenerator Resolve(string model, int dimensions)
    {
        ValidateProviderConfigured();
        return _cache.GetOrAdd((model, dimensions), key => _factories[_options.Provider].Create(_options, key.Model, key.Dimensions));
    }
}
