using Shouldly;
using Xunit;

namespace CodeCiir.Reranking.Abstraction.Tests;

public sealed class NoOpRerankerTests
{
    [Fact]
    public async Task RerankAsync_ReturnsEveryCandidateUnscoredInOriginalOrder()
    {
        var sut = new NoOpReranker();
        var candidates = new[]
        {
            new RerankCandidate(1, "first"),
            new RerankCandidate(2, "second"),
            new RerankCandidate(3, "third"),
        };

        var result = await sut.RerankAsync("question", candidates);

        result.Select(r => r.Id).ShouldBe([1, 2, 3]);
        result.ShouldAllBe(r => r.Score == null);
    }

    [Fact]
    public void Provider_IsNone()
    {
        new NoOpReranker().Provider.ShouldBe("None");
    }

    [Fact]
    public void CandidatePoolSize_IsZero()
    {
        new NoOpReranker().CandidatePoolSize.ShouldBe(0);
    }
}
