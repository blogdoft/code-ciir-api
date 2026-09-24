using Bogus;
using CodeCiir.Application.CodeQueries;
using CodeCiir.Application.Feedback;
using CodeCiir.Mcp.Tools;
using NSubstitute;

namespace CodeCiir.Mcp.Tests.CodeQueryToolsTests;

public abstract class BaseCodeQueryToolsTests
{
    protected ICodeQueryService CodeQueryService { get; } = Substitute.For<ICodeQueryService>();

    protected IFeedbackService FeedbackService { get; } = Substitute.For<IFeedbackService>();

    protected ICodeDocumentSourceService CodeDocumentSourceService { get; } = Substitute.For<ICodeDocumentSourceService>();

    protected Faker Faker { get; } = new();

    protected CodeQueryTools Sut => new(CodeQueryService, FeedbackService, CodeDocumentSourceService);
}
