using CodeCiir.Reranking.Abstraction;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Reranking.OpenAI.Tests;

public sealed class OpenAIRerankerProviderFactoryTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly OpenAIRerankerProviderFactory _sut;
    private HttpClient? _createdClient;

    public OpenAIRerankerProviderFactoryTests()
    {
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => _createdClient = new HttpClient());
        _sut = new OpenAIRerankerProviderFactory(_httpClientFactory);
    }

    [Fact]
    public void ProviderName_IsOpenAI()
    {
        _sut.ProviderName.ShouldBe("OpenAI");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingModel_Throws(string? model)
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = model! };

        Should.Throw<InvalidOperationException>(() => _sut.Create(options));
    }

    [Fact]
    public void Create_BaseUrlOmitted_DefaultsToOpenAiApi()
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = "gpt-4o-mini" };

        _sut.Create(options);

        _createdClient!.BaseAddress.ShouldBe(new Uri("https://api.openai.com/v1/"));
    }

    [Fact]
    public void Create_ApiKeyProvided_SetsAuthorizationHeader()
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = "gpt-4o-mini", ApiKey = "sk-test" };

        _sut.Create(options);

        _createdClient!.DefaultRequestHeaders.Authorization.ShouldNotBeNull();
        _createdClient.DefaultRequestHeaders.Authorization.Parameter.ShouldBe("sk-test");
    }

    [Fact]
    public void Create_ApiKeyOmitted_DoesNotSetAuthorizationHeader()
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = "gpt-4o-mini" };

        _sut.Create(options);

        _createdClient!.DefaultRequestHeaders.Authorization.ShouldBeNull();
    }

    [Fact]
    public void Create_ValidOptions_ReturnsConfiguredReranker()
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = "gpt-4o-mini", CandidatePoolSize = 30 };

        var reranker = _sut.Create(options);

        reranker.Provider.ShouldBe("OpenAI");
        reranker.CandidatePoolSize.ShouldBe(30);
    }
}
