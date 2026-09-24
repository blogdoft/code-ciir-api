using Bogus;
using Shouldly;
using Xunit;

namespace CodeCiir.Reranking.Abstraction.Tests;

public sealed class NoOpRerankerTests
{
    private static readonly Faker<RerankCandidate> CandidateFaker = new Faker<RerankCandidate>()
        .CustomInstantiator(faker => new RerankCandidate(faker.Random.Long(1, 100_000), faker.Lorem.Sentence()));

    private readonly NoOpReranker _sut = new();

    [Fact]
    public async Task Should_ReturnEveryCandidateUnscoredInOriginalOrder_When_Reranking()
    {
        var candidates = CandidateFaker.Generate(3);

        var result = await _sut.RerankAsync("question", candidates);

        result.Select(r => (r.Id, r.Score)).ShouldBe(candidates.Select(c => (c.Id, (double?)null)));
    }

    [Fact]
    public void Should_ReportNoneAsProvider_When_Queried()
    {
        _sut.Provider.ShouldBe("None");
    }

    [Fact]
    public void Should_ReportZeroCandidatePoolSize_When_Queried()
    {
        _sut.CandidatePoolSize.ShouldBe(0);
    }
}
