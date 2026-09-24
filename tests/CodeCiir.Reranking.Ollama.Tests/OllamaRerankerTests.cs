using Bogus;
using CodeCiir.Reranking.Abstraction;
using Shouldly;
using System.Net;
using Xunit;

namespace CodeCiir.Reranking.Ollama.Tests;

public sealed class OllamaRerankerTests
{
    private const string Model = "qwen2.5:7b-instruct";

    private static readonly Faker<RerankCandidate> CandidateFaker = new Faker<RerankCandidate>()
        .CustomInstantiator(faker => new RerankCandidate(faker.Random.Long(1, 100_000), faker.Lorem.Sentence()));

    [Fact]
    public async Task Should_ReturnNormalizedScore_When_ResponseIsSuccessful()
    {
        var candidate = CandidateFaker.Generate();
        var sut = CreateSut(new FakeHttpMessageHandler(_ => JsonResponse("""{"response":"{\"score\": 70000}"}""")));

        var result = await sut.RerankAsync("question", [candidate]);

        result.Select(r => (r.Id, r.Score)).ShouldBe([(candidate.Id, (double?)0.7)]);
    }

    [Fact]
    public async Task Should_KeepFiveDecimalDigitsOfResolution_When_ScoreIsNormalized()
    {
        var sut = CreateSut(new FakeHttpMessageHandler(_ => JsonResponse("""{"response":"{\"score\": 12345}"}""")));

        var result = await sut.RerankAsync("question", [CandidateFaker.Generate()]);

        result.Single().Score.ShouldBe(0.12345);
    }

    [Fact]
    public async Task Should_PreserveInputOrder_When_ThereAreMultipleCandidates()
    {
        var scores = new Queue<string>(["30000", "90000", "10000"]);
        var sut = CreateSut(new FakeHttpMessageHandler(_ => JsonResponse($$"""{"response":"{\"score\": {{scores.Dequeue()}}}"}""")));
        var candidates = CandidateFaker.Generate(3);

        var result = await sut.RerankAsync("question", candidates);

        result.Select(r => r.Id).ShouldBe(candidates.Select(c => c.Id));
    }

    [Fact]
    public async Task Should_SendModelQuestionAndCandidateText_When_Reranking()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse("""{"response":"{\"score\": 5}"}"""));
        var sut = CreateSut(handler);

        await sut.RerankAsync("where is the retry logic?", [new RerankCandidate(1, "public void Retry() {}")]);

        handler.LastRequestBody.ShouldNotBeNull();
        handler.LastRequestBody.ShouldContain(Model);
        handler.LastRequestBody.ShouldContain("where is the retry logic?");
        handler.LastRequestBody.ShouldContain("public void Retry() {}");
    }

    [Fact]
    public async Task Should_ThrowRerankingException_When_ServerReturnsAnError()
    {
        var sut = CreateSut(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Should.ThrowAsync<RerankingException>(() => sut.RerankAsync("question", [CandidateFaker.Generate()]));
    }

    [Fact]
    public async Task Should_ThrowRerankingException_When_ResponseHasNoScorePayload()
    {
        var sut = CreateSut(new FakeHttpMessageHandler(_ => JsonResponse("""{"response":"not json at all"}""")));

        await Should.ThrowAsync<RerankingException>(() => sut.RerankAsync("question", [CandidateFaker.Generate()]));
    }

    [Fact]
    public async Task Should_ThrowRerankingException_When_TopLevelJsonIsMalformed()
    {
        var sut = CreateSut(new FakeHttpMessageHandler(_ => JsonResponse("not json")));

        await Should.ThrowAsync<RerankingException>(() => sut.RerankAsync("question", [CandidateFaker.Generate()]));
    }

    [Fact]
    public void Should_ReportConfiguredCandidatePoolSize_When_Queried()
    {
        var sut = CreateSut(new FakeHttpMessageHandler(_ => JsonResponse("{}")), candidatePoolSize: 42);

        sut.CandidatePoolSize.ShouldBe(42);
    }

    private static OllamaReranker CreateSut(FakeHttpMessageHandler handler, int candidatePoolSize = 25) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://fake/") }, Model, candidatePoolSize, 6);

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };
}
