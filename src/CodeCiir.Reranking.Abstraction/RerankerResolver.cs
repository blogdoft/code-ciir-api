using Microsoft.Extensions.Options;

namespace CodeCiir.Reranking.Abstraction;

/// <summary>
/// Picks the <see cref="IRerankerProviderFactory"/> matching the configured provider and builds
/// the resulting <see cref="IReranker"/>. Unlike <c>EmbeddingGeneratorResolver</c>, an empty or
/// "None" <see cref="RerankingOptions.Provider"/> is a valid, supported "disabled" state -
/// <see cref="Resolve"/> returns a <see cref="NoOpReranker"/> rather than throwing. Only an
/// unknown, non-empty provider name (a configuration typo) throws.
/// </summary>
public sealed class RerankerResolver
{
    private const string NoneProviderName = "None";

    private readonly IReadOnlyDictionary<string, IRerankerProviderFactory> _factories;
    private readonly RerankingOptions _options;

    public RerankerResolver(IEnumerable<IRerankerProviderFactory> factories, IOptions<RerankingOptions> options)
    {
        _factories = factories.ToDictionary(f => f.ProviderName, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
    }

    /// <summary>Resolves the configured reranker, or a <see cref="NoOpReranker"/> when reranking is disabled.</summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="RerankingOptions.Provider"/> is non-empty, not "None", and does not match any
    /// registered <see cref="IRerankerProviderFactory"/>.
    /// </exception>
    public IReranker Resolve()
    {
        if (string.IsNullOrWhiteSpace(_options.Provider) || string.Equals(_options.Provider, NoneProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return new NoOpReranker();
        }

        if (!_factories.TryGetValue(_options.Provider, out var factory))
        {
            throw new InvalidOperationException(
                $"Unknown reranking provider '{_options.Provider}'. Available providers: " +
                string.Join(", ", _factories.Keys) + $", or leave '{RerankingOptions.SectionName}:Provider' empty to disable reranking.");
        }

        return factory.Create(_options);
    }
}
