using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Application.CodeQueries;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Shouldly;
using System.ComponentModel;
using System.Reflection;
using Xunit;

namespace CodeCiir.Mcp.Tests.CodeQueryToolsTests;

public sealed class GetCodeSourceAsyncTests : BaseCodeQueryToolsTests
{
    [Fact]
    public async Task Should_MapSourceLocators_When_ServiceSucceeds()
    {
        var documentId = Faker.Random.Long(1, 1000);
        var source = new CodeDocumentSource(documentId, Faker.System.FilePath(), new Uri(Faker.Internet.UrlWithPath()));
        CodeDocumentSourceService.GetAsync(documentId, Arg.Any<CancellationToken>())
            .Returns(Result<CodeDocumentSource>.FromSuccess(source));

        var result = await Sut.GetCodeSourceAsync(documentId);

        result.DocumentId.ShouldBe(documentId);
        result.SourceFile.ShouldBe(source.SourceFile);
        result.GitRawUrl.ShouldBe(source.GitRawUrl);
    }

    [Fact]
    public async Task Should_ThrowMcpExceptionWithTheFailureMessage_When_ServiceFails()
    {
        var documentId = Faker.Random.Long(1, 1000);
        var failure = CodeQueryFailures.CodeDocumentNotFound(documentId);
        CodeDocumentSourceService.GetAsync(documentId, Arg.Any<CancellationToken>())
            .Returns(Result<CodeDocumentSource>.FromFailure(failure));

        var exception = await Should.ThrowAsync<McpException>(() => Sut.GetCodeSourceAsync(documentId));

        exception.Message.ShouldContain(failure.Message);
    }

    [Fact]
    public void Should_NameTheToolAndDescribeTheQueryProjectCodeWorkflow_When_ToolsAreDiscovered()
    {
        var method = typeof(Tools.CodeQueryTools).GetMethod(nameof(Tools.CodeQueryTools.GetCodeSourceAsync));

        method.ShouldNotBeNull();
        method.GetCustomAttribute<McpServerToolAttribute>()!.Name.ShouldBe("get_code_source");
        method.GetCustomAttribute<DescriptionAttribute>()!.Description.ShouldContain("query_project_code");
        method.GetCustomAttribute<DescriptionAttribute>()!.Description.ShouldContain("matches[].id");
    }
}
