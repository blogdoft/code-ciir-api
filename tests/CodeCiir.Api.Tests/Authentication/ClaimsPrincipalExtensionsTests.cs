using CodeCiir.Api.Authentication;
using Shouldly;
using System.Security.Claims;
using Xunit;

namespace CodeCiir.Api.Tests.Authentication;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void Should_PreferThePreferredUsernameClaim_When_SeveralIdentityClaimsExist()
    {
        var principal = Principal(("preferred_username", "maria"), ("name", "Maria Silva"), ("sub", "abc-123"));

        principal.GetUsername().ShouldBe("maria");
    }

    [Fact]
    public void Should_FallBackToTheNameClaim_When_PreferredUsernameIsMissing()
    {
        var principal = Principal(("name", "Maria Silva"), ("sub", "abc-123"));

        principal.GetUsername().ShouldBe("Maria Silva");
    }

    [Fact]
    public void Should_FallBackToTheSubjectClaim_When_NoNameClaimExists()
    {
        var principal = Principal(("sub", "abc-123"));

        principal.GetUsername().ShouldBe("abc-123");
    }

    [Fact]
    public void Should_ReturnAnonymous_When_ThePrincipalHasNoIdentityClaim()
    {
        Principal().GetUsername().ShouldBe("anonymous");
    }

    [Fact]
    public void Should_ReturnAnonymous_When_ThePrincipalIsNull()
    {
        ((ClaimsPrincipal?)null).GetUsername().ShouldBe("anonymous");
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));
}
