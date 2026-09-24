using CodeCiir.Reranking.Abstraction;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Reranking.Ollama.Tests;

public sealed class OllamaRerankerProviderFactoryTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();

    public OllamaRerankerProviderFactoryTests()
    {
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient());
    }

    private OllamaRerankerProviderFactory Sut => new(_httpClientFactory);

    [Fact]
    public void Should_ReportOllamaAsProviderName_When_Queried()
    {
        Sut.ProviderName.ShouldBe("Ollama");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowInvalidOperationException_When_BaseUrlIsMissing(string? baseUrl)
    {
        var options = new RerankingOptions { Provider = "Ollama", Model = "qwen2.5:7b-instruct", BaseUrl = baseUrl };

        Should.Throw<InvalidOperationException>(() => Sut.Create(options));
    }

    [Fact]
    public void Should_ReturnRerankerConfiguredFromOptions_When_OptionsAreValid()
    {
        var options = new RerankingOptions
        {
            Provider = "Ollama",
            Model = "qwen2.5:7b-instruct",
            BaseUrl = "http://192.168.1.212:11434",
            CandidatePoolSize = 30,
            MaxConcurrency = 4,
        };

        var reranker = Sut.Create(options);

        reranker.Provider.ShouldBe("Ollama");
        reranker.CandidatePoolSize.ShouldBe(30);
    }
}
