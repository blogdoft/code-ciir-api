using CodeCiir.Api.Filters;
using CodeCiir.Api.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeCiir.Api.Tests.Filters;

public sealed class UnhandledExceptionFilterTests : IDisposable
{
    private readonly CapturingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;

    public UnhandledExceptionFilterTests()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
    }

    public void Dispose() => _loggerFactory.Dispose();

    [Fact]
    public void Should_ReplyWithABareInternalServerErrorAndMarkTheExceptionHandled_When_AnActionThrows()
    {
        var context = CreateContext(new InvalidOperationException("Password=secret"));

        CreateSut().OnException(context);

        context.ExceptionHandled.ShouldBeTrue();
        context.Result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void Should_LogTheExceptionAtErrorLevelWithTheRequestLine_When_AnActionThrows()
    {
        var context = CreateContext(new InvalidOperationException("boom"), "POST", "/api/code-queries");

        CreateSut().OnException(context);

        var entry = _logs.Entries.ShouldHaveSingleItem();
        (entry.Level, entry.Message).ShouldBe((LogLevel.Error, "Unhandled exception while processing POST /api/code-queries"));
    }

    private UnhandledExceptionFilter CreateSut() => new(_loggerFactory.CreateLogger<UnhandledExceptionFilter>());

    private ExceptionContext CreateContext(Exception exception, string method = "GET", string path = "/api/test") => new(
        new ActionContext(HttpContextFactory.Create(_logs, method, path), new RouteData(), new ActionDescriptor()),
        [])
    {
        Exception = exception,
    };
}
