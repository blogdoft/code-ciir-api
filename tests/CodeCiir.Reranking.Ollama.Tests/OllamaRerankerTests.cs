using CodeCiir.Reranking.Abstraction;
using Shouldly;
using System.Net;
using Xunit;

namespace CodeCiir.Reranking.Ollama.Tests;

public sealed class OllamaRerankerTests
{
    [Fact]
    public async Task RerankAsync_SuccessfulResponse_ReturnsNormalizedScore()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse("""{"response":"{\"score\": 70000}"}"""));
        var sut = CreateSut(handler);

        var result = await sut.RerankAsync("question", [new RerankCandidate(1, "some code")]);

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe(1);
        result[0].Score.ShouldBe(0.7);
    }

    [Fact]
    public async Task RerankAsync_SuccessfulResponse_ScoreHasFiveDecimalDigitsOfResolution()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse("""{"response":"{\"score\": 12345}"}"""));
        var sut = CreateSut(handler);

        var result = await sut.RerankAsync("question", [new RerankCandidate(1, "some code")]);

        result[0].Score.ShouldBe(0.12345);
    }

    [Fact]
    public async Task RerankAsync_MultipleCandidates_PreservesInputOrder()
    {
        var scores = new Queue<string>(["30000", "90000", "10000"]);
        var handler = new FakeHttpMessageHandler(_ => JsonResponse($$"""{"response":"{\"score\": {{scores.Dequeue()}}}"}"""));
        var sut = CreateSut(handler);
        var candidates = new[] { new RerankCandidate(1, "a"), new RerankCandidate(2, "b"), new RerankCandidate(3, "c") };

        var result = await sut.RerankAsync("question", candidates);

        result.Select(r => r.Id).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task RerankAsync_SendsModelAndQuestionAndCandidateText()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse("""{"response":"{\"score\": 5}"}"""));
        var sut = new OllamaReranker(new HttpClient(handler) { BaseAddress = new Uri("http://fake/") }, "qwen2.5:7b-instruct", 25, 6);

        await sut.RerankAsync("where is the retry logic?", [new RerankCandidate(1, "public void Retry() {}")]);

        handler.LastRequestBody.ShouldNotBeNull();
        handler.LastRequestBody.ShouldContain("qwen2.5:7b-instruct");
        handler.LastRequestBody.ShouldContain("where is the retry logic?");
        handler.LastRequestBody.ShouldContain("public void Retry() {}");
    }

    [Fact]
    public async Task RerankAsync_ServerError_ThrowsRerankingException()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<RerankingException>(() => sut.RerankAsync("question", [new RerankCandidate(1, "code")]));
    }

    [Fact]
    public async Task RerankAsync_ResponseHasNoScorePayload_ThrowsRerankingException()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse("""{"response":"not json at all"}"""));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<RerankingException>(() => sut.RerankAsync("question", [new RerankCandidate(1, "code")]));
    }

    [Fact]
    public async Task RerankAsync_MalformedTopLevelJson_ThrowsRerankingException()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse("not json"));
        var sut = CreateSut(handler);

        await Should.ThrowAsync<RerankingException>(() => sut.RerankAsync("question", [new RerankCandidate(1, "code")]));
    }

    [Fact]
    public void CandidatePoolSize_ReportsConfiguredValue()
    {
        var sut = new OllamaReranker(new HttpClient(new FakeHttpMessageHandler(_ => JsonResponse("{}"))) { BaseAddress = new Uri("http://fake/") }, "model", 42, 6);

        sut.CandidatePoolSize.ShouldBe(42);
    }

    private static OllamaReranker CreateSut(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://fake/") }, "qwen2.5:7b-instruct", 25, 6);

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };
}
