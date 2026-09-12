using CodeCiir.Application.CodeQueries;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.CodeQueries;

public sealed class CodeDocumentSourceServiceTests
{
    private readonly ICodeDocumentsRepository _repository = Substitute.For<ICodeDocumentsRepository>();
    private readonly CodeDocumentSourceService _sut;

    public CodeDocumentSourceServiceTests()
    {
        _sut = new CodeDocumentSourceService(_repository);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAsync_InvalidDocumentId_ReturnsInvalidFailureWithoutQueryingRepository(long documentId)
    {
        var result = await _sut.GetAsync(documentId);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("400-code-document-id-invalid");
        await _repository.DidNotReceive().GetSourceAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_MissingDocument_ReturnsNotFoundFailure()
    {
        _repository.GetSourceAsync(42, Arg.Any<CancellationToken>()).Returns((CodeDocumentSource?)null);

        var result = await _sut.GetAsync(42);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("404-code-document-not-found");
    }

    [Fact]
    public async Task GetAsync_ExistingDocument_ReturnsSourceIncludingNullRawUrl()
    {
        var source = new CodeDocumentSource(42, "src/Widget.cs", null);
        _repository.GetSourceAsync(42, Arg.Any<CancellationToken>()).Returns(source);

        var result = await _sut.GetAsync(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(source);
    }
}
