using Bogus;
using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Tests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CodeCiir.Application.Tests.CodeQueries;

public sealed class CodeDocumentSourceServiceTests
{
    private readonly ICodeDocumentsRepository _repository = Substitute.For<ICodeDocumentsRepository>();
    private readonly Faker _faker = new();

    private CodeDocumentSourceService Sut => new(_repository);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Should_ReturnInvalidIdFailureWithoutQueryingRepository_When_DocumentIdIsNotPositive(long documentId)
    {
        var result = await Sut.GetAsync(documentId);

        result.ShouldBeFailure(CodeQueryFailures.CodeDocumentIdInvalid());
        await _repository.DidNotReceive().GetSourceAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ReturnNotFoundFailure_When_DocumentDoesNotExist()
    {
        var documentId = _faker.Random.Long(1, 1000);
        _repository.GetSourceAsync(documentId, Arg.Any<CancellationToken>()).Returns((CodeDocumentSource?)null);

        var result = await Sut.GetAsync(documentId);

        result.ShouldBeFailure(CodeQueryFailures.CodeDocumentNotFound(documentId));
    }

    [Fact]
    public async Task Should_ReturnSourceWithNullRawUrl_When_ProjectHasNoRawUrl()
    {
        var documentId = _faker.Random.Long(1, 1000);
        var source = new CodeDocumentSource(documentId, _faker.System.FilePath(), null);
        _repository.GetSourceAsync(documentId, Arg.Any<CancellationToken>()).Returns(source);

        var result = await Sut.GetAsync(documentId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(source);
    }
}
