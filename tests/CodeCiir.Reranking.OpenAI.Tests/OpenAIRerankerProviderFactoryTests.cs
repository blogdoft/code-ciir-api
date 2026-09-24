using CodeCiir.Reranking.Abstraction;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Reranking.OpenAI.Tests;

public sealed class OpenAIRerankerProviderFactoryTests
{
    private const string Model = "gpt-4o-mini";

    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private HttpClient? _createdClient;

    public OpenAIRerankerProviderFactoryTests()
    {
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => _createdClient = new HttpClient());
    }

    private OpenAIRerankerProviderFactory Sut => new(_httpClientFactory);

    [Fact]
    public void Should_ReportOpenAIAsProviderName_When_Queried()
    {
        Sut.ProviderName.ShouldBe("OpenAI");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowInvalidOperationException_When_ModelIsMissing(string? model)
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = model! };

        Should.Throw<InvalidOperationException>(() => Sut.Create(options));
    }

    [Fact]
    public void Should_DefaultToTheOpenAiApi_When_BaseUrlIsOmitted()
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = Model };

        Sut.Create(options);

        _createdClient!.BaseAddress.ShouldBe(new Uri("https://api.openai.com/v1/"));
    }

    [Fact]
    public void Should_SetTheBearerAuthorizationHeader_When_ApiKeyIsProvided()
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = Model, ApiKey = "sk-test" };

        Sut.Create(options);

        _createdClient!.DefaultRequestHeaders.Authorization.ShouldNotBeNull();
        _createdClient.DefaultRequestHeaders.Authorization.Parameter.ShouldBe("sk-test");
    }

    [Fact]
    public void Should_NotSetAnAuthorizationHeader_When_ApiKeyIsOmitted()
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = Model };

        Sut.Create(options);

        _createdClient!.DefaultRequestHeaders.Authorization.ShouldBeNull();
    }

    [Fact]
    public void Should_ReturnRerankerConfiguredFromOptions_When_OptionsAreValid()
    {
        var options = new RerankingOptions { Provider = "OpenAI", Model = Model, CandidatePoolSize = 30 };

        var reranker = Sut.Create(options);

        reranker.Provider.ShouldBe("OpenAI");
        reranker.CandidatePoolSize.ShouldBe(30);
    }
}
