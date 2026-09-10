namespace CodeCiir.Embeddings.Abstraction;

/// <summary>
/// A dense vector produced by an embedding provider for a single piece of text.
/// </summary>
/// <param name="Values">The raw embedding components, in provider-defined order.</param>
public sealed record EmbeddingVector(IReadOnlyList<float> Values)
{
    public int Dimensions => Values.Count;
}
