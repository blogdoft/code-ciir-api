using CodeCiir.Reranking.Abstraction;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Reranking.Abstraction.Tests;

public sealed class RerankerResolverTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Resolve_ProviderEmpty_ReturnsNoOpReranker(string? provider)
    {
        var resolver = new RerankerResolver([], Options.Create(new RerankingOptions { Provider = provider! }));

        var result = resolver.Resolve();

        result.Provider.ShouldBe("None");
        result.CandidatePoolSize.ShouldBe(0);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("NONE")]
    public void Resolve_ProviderIsNone_ReturnsNoOpReranker(string provider)
    {
        var resolver = new RerankerResolver([], Options.Create(new RerankingOptions { Provider = provider }));

        var result = resolver.Resolve();

        result.Provider.ShouldBe("None");
    }

    [Fact]
    public void Resolve_UnknownProvider_ThrowsInvalidOperationException()
    {
        var resolver = new RerankerResolver([], Options.Create(new RerankingOptions { Provider = "DoesNotExist" }));

        Should.Throw<InvalidOperationException>(() => resolver.Resolve());
    }

    [Fact]
    public void Resolve_KnownProvider_ReturnsFactoryResult()
    {
        var reranker = Substitute.For<IReranker>();
        var factory = Substitute.For<IRerankerProviderFactory>();
        factory.ProviderName.Returns("Ollama");
        factory.Create(Arg.Any<RerankingOptions>()).Returns(reranker);
        var resolver = new RerankerResolver([factory], Options.Create(new RerankingOptions { Provider = "Ollama" }));

        var result = resolver.Resolve();

        result.ShouldBeSameAs(reranker);
    }

    [Fact]
    public void Resolve_ProviderMatchedCaseInsensitively()
    {
        var reranker = Substitute.For<IReranker>();
        var factory = Substitute.For<IRerankerProviderFactory>();
        factory.ProviderName.Returns("Ollama");
        factory.Create(Arg.Any<RerankingOptions>()).Returns(reranker);
        var resolver = new RerankerResolver([factory], Options.Create(new RerankingOptions { Provider = "OLLAMA" }));

        var result = resolver.Resolve();

        result.ShouldBeSameAs(reranker);
    }
}
