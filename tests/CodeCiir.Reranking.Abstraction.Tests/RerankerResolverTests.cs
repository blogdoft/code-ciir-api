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
    public void Should_ReturnNoOpReranker_When_ProviderIsEmpty(string? provider)
    {
        var sut = CreateSut(provider!);

        var result = sut.Resolve();

        result.ShouldBeOfType<NoOpReranker>();
    }

    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("NONE")]
    public void Should_ReturnNoOpReranker_When_ProviderIsNoneInAnyCasing(string provider)
    {
        var sut = CreateSut(provider);

        var result = sut.Resolve();

        result.ShouldBeOfType<NoOpReranker>();
    }

    [Fact]
    public void Should_ThrowInvalidOperationExceptionNamingTheProvider_When_ProviderIsUnknown()
    {
        var sut = CreateSut("DoesNotExist");

        var exception = Should.Throw<InvalidOperationException>(() => sut.Resolve());

        exception.Message.ShouldContain("DoesNotExist");
    }

    [Fact]
    public void Should_ReturnTheFactoryResult_When_ProviderIsKnown()
    {
        var reranker = Substitute.For<IReranker>();
        var sut = CreateSut("Ollama", CreateFactory("Ollama", reranker));

        var result = sut.Resolve();

        result.ShouldBeSameAs(reranker);
    }

    [Fact]
    public void Should_MatchTheProviderCaseInsensitively_When_ConfiguredNameDiffersInCasing()
    {
        var reranker = Substitute.For<IReranker>();
        var sut = CreateSut("OLLAMA", CreateFactory("Ollama", reranker));

        var result = sut.Resolve();

        result.ShouldBeSameAs(reranker);
    }

    private static RerankerResolver CreateSut(string provider, params IRerankerProviderFactory[] factories) =>
        new(factories, Options.Create(new RerankingOptions { Provider = provider }));

    private static IRerankerProviderFactory CreateFactory(string providerName, IReranker reranker)
    {
        var factory = Substitute.For<IRerankerProviderFactory>();
        factory.ProviderName.Returns(providerName);
        factory.Create(Arg.Any<RerankingOptions>()).Returns(reranker);
        return factory;
    }
}
