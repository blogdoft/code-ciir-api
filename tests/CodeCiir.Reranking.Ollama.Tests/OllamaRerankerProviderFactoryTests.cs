using CodeCiir.Reranking.Abstraction;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Reranking.Ollama.Tests;

public sealed class OllamaRerankerProviderFactoryTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly OllamaRerankerProviderFactory _sut;

    public OllamaRerankerProviderFactoryTests()
    {
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient());
        _sut = new OllamaRerankerProviderFactory(_httpClientFactory);
    }

    [Fact]
    public void ProviderName_IsOllama()
    {
        _sut.ProviderName.ShouldBe("Ollama");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingBaseUrl_Throws(string? baseUrl)
    {
        var options = new RerankingOptions { Provider = "Ollama", Model = "qwen2.5:7b-instruct", BaseUrl = baseUrl };

        Should.Throw<InvalidOperationException>(() => _sut.Create(options));
    }

    [Fact]
    public void Create_ValidOptions_ReturnsConfiguredReranker()
    {
        var options = new RerankingOptions
        {
            Provider = "Ollama",
            Model = "qwen2.5:7b-instruct",
            BaseUrl = "http://192.168.1.212:11434",
            CandidatePoolSize = 30,
            MaxConcurrency = 4,
        };

        var reranker = _sut.Create(options);

        reranker.Provider.ShouldBe("Ollama");
        reranker.CandidatePoolSize.ShouldBe(30);
    }
}
