using BlogDoFT.Libs.ResultPattern;
using CodeCiir.Api.Problems;
using CodeCiir.Api.Tests.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shouldly;
using System.Security.Claims;
using Xunit;

namespace CodeCiir.Api.Tests.Problems;

public sealed class FailureResultsTests
{
    private readonly CapturingLoggerProvider _logs = new();

    [Fact]
    public void Should_ReturnProblemDetailsWithTheFailureMessage_When_StatusIsBadRequest()
    {
        var failure = new Failure("400-something-invalid", "Something is invalid.");

        var result = failure.ToActionResult(HttpContextFactory.Create(_logs, path: "/api/things"));

        var problem = result.ShouldBeOfType<ObjectResult>().Value.ShouldBeOfType<ProblemDetails>();
        (problem.Status, problem.Title, problem.Detail, problem.Instance).ShouldBe((400, "Bad Request", "Something is invalid.", "/api/things"));
    }

    [Theory]
    [InlineData("401-unauthenticated", 401)]
    [InlineData("403-forbidden", 403)]
    [InlineData("404-project-not-found", 404)]
    [InlineData("500-broken", 500)]
    [InlineData("503-unavailable", 503)]
    public void Should_ReturnABodylessStatusCode_When_StatusIsUnauthorizedForbiddenNotFoundOrServerError(string code, int expectedStatus)
    {
        var result = new Failure(code, "some detail that must not leak").ToActionResult(HttpContextFactory.Create(_logs));

        result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(expectedStatus);
    }

    [Fact]
    public void Should_DefaultToBadRequest_When_TheCodeHasNoLeadingStatus()
    {
        var result = new Failure("something-odd", "Odd.").ToActionResult(HttpContextFactory.Create(_logs));

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(400);
    }

    [Fact]
    public void Should_LogTheUsernameAsAWarning_When_StatusIsForbidden()
    {
        var context = HttpContextFactory.Create(_logs, "GET", "/api/things", new Claim("preferred_username", "maria"));

        new Failure("403-forbidden", "Not allowed.").ToActionResult(context);

        var entry = _logs.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("maria");
    }

    [Fact]
    public void Should_LogAtInformationLevelWithoutAnyUsername_When_StatusIsNotFound()
    {
        var context = HttpContextFactory.Create(_logs, "GET", "/api/things", new Claim("preferred_username", "maria"));

        new Failure("404-project-not-found", "No project.").ToActionResult(context);

        var entry = _logs.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldNotContain("maria");
    }

    [Fact]
    public void Should_LogAtErrorLevel_When_StatusIsAServerError()
    {
        new Failure("500-broken", "Broken.").ToActionResult(HttpContextFactory.Create(_logs));

        _logs.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public void Should_NotLogAnything_When_StatusIsBadRequest()
    {
        new Failure("400-something-invalid", "Invalid.").ToActionResult(HttpContextFactory.Create(_logs));

        _logs.Entries.ShouldBeEmpty();
    }
}
