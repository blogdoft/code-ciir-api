using CodeCiir.Api.Authentication;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace CodeCiir.Api.Tests.Authentication;

public sealed class KeycloakOptionsTests
{
    [Fact]
    public void Should_ReturnNull_When_ThereIsNoKeycloakSection()
    {
        KeycloakOptions.FromConfiguration(Configure([])).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public void Should_ReturnNullEvenWithTheRealmFullyConfigured_When_KeycloakIsNotEnabled(string? enabled)
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = enabled,
            ["Keycloak:Authority"] = "https://keycloak.example/realms/blogdoft",
            ["Keycloak:Audience"] = "code-ciir-api",
            ["Keycloak:ClientId"] = "swagger",
        });

        KeycloakOptions.FromConfiguration(configuration).ShouldBeNull();
    }

    [Fact]
    public void Should_NotValidateTheRestOfTheSection_When_KeycloakIsNotEnabled()
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "false",
            ["Keycloak:Authority"] = "not-a-url",
        });

        KeycloakOptions.FromConfiguration(configuration).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowNamingTheAuthorityKey_When_EnabledWithoutAuthority(string? authority)
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "true",
            ["Keycloak:Authority"] = authority,
        });

        var exception = Should.Throw<InvalidOperationException>(() => KeycloakOptions.FromConfiguration(configuration));

        exception.Message.ShouldContain("Keycloak:Authority");
    }

    [Theory]
    [InlineData("realms/blogdoft")]
    [InlineData("ftp://keycloak.example/realms/blogdoft")]
    public void Should_ThrowInvalidOperationException_When_AuthorityIsNotAnAbsoluteHttpUrl(string authority)
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "true",
            ["Keycloak:Authority"] = authority,
        });

        Should.Throw<InvalidOperationException>(() => KeycloakOptions.FromConfiguration(configuration));
    }

    [Fact]
    public void Should_ThrowNamingRequireHttpsMetadata_When_HttpAuthorityIsUsedWhileHttpsMetadataIsRequired()
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "true",
            ["Keycloak:Authority"] = "http://localhost:8080/realms/blogdoft",
        });

        var exception = Should.Throw<InvalidOperationException>(() => KeycloakOptions.FromConfiguration(configuration));

        exception.Message.ShouldContain("RequireHttpsMetadata");
    }

    [Fact]
    public void Should_ReturnOptions_When_HttpAuthorityIsUsedWithoutRequiringHttpsMetadata()
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "true",
            ["Keycloak:Authority"] = "http://localhost:8080/realms/blogdoft",
            ["Keycloak:RequireHttpsMetadata"] = "false",
        });

        var options = KeycloakOptions.FromConfiguration(configuration);

        options.ShouldNotBeNull();
        options.RequireHttpsMetadata.ShouldBeFalse();
    }

    [Fact]
    public void Should_ReturnTrimmedOptions_When_EnabledWithFullConfiguration()
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "true",
            ["Keycloak:Authority"] = "  https://keycloak.example/realms/blogdoft ",
            ["Keycloak:Audience"] = " code-ciir-api ",
            ["Keycloak:ClientId"] = " swagger ",
            ["Keycloak:MetadataAddress"] = " http://keycloak.internal.svc.cluster.local:8080/realms/blogdoft/.well-known/openid-configuration ",
            ["Keycloak:RequireHttpsMetadata"] = "false",
            ["Keycloak:SkipCertificateValidation"] = "true",
        });

        var options = KeycloakOptions.FromConfiguration(configuration);

        options.ShouldNotBeNull();
        options.Enabled.ShouldBeTrue();
        options.Authority.ShouldBe("https://keycloak.example/realms/blogdoft");
        options.Audience.ShouldBe("code-ciir-api");
        options.ClientId.ShouldBe("swagger");
        options.MetadataAddress.ShouldBe("http://keycloak.internal.svc.cluster.local:8080/realms/blogdoft/.well-known/openid-configuration");
        options.RequireHttpsMetadata.ShouldBeFalse();
        options.SkipCertificateValidation.ShouldBeTrue();
    }

    [Fact]
    public void Should_DefaultSkipCertificateValidationToFalse_When_ItIsNotConfigured()
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "true",
            ["Keycloak:Authority"] = "https://keycloak.example/realms/blogdoft",
        });

        var options = KeycloakOptions.FromConfiguration(configuration);

        options.ShouldNotBeNull();
        options.SkipCertificateValidation.ShouldBeFalse();
    }

    [Theory]
    [InlineData("realms/blogdoft")]
    [InlineData("ftp://keycloak.internal/realms/blogdoft/.well-known/openid-configuration")]
    public void Should_ThrowNamingTheMetadataAddressKey_When_MetadataAddressIsNotAnAbsoluteHttpUrl(string metadataAddress)
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "true",
            ["Keycloak:Authority"] = "https://keycloak.example/realms/blogdoft",
            ["Keycloak:MetadataAddress"] = metadataAddress,
        });

        var exception = Should.Throw<InvalidOperationException>(() => KeycloakOptions.FromConfiguration(configuration));

        exception.Message.ShouldContain("Keycloak:MetadataAddress");
    }

    [Fact]
    public void Should_ThrowNamingBothKeys_When_HttpMetadataAddressIsUsedWhileHttpsMetadataIsRequired()
    {
        // Authority itself stays https here - it's the metadata address's own scheme that matters
        // once it's set, not Authority's.
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "true",
            ["Keycloak:Authority"] = "https://keycloak.example/realms/blogdoft",
            ["Keycloak:MetadataAddress"] = "http://keycloak.internal.svc.cluster.local:8080/realms/blogdoft/.well-known/openid-configuration",
        });

        var exception = Should.Throw<InvalidOperationException>(() => KeycloakOptions.FromConfiguration(configuration));

        exception.Message.ShouldContain("Keycloak:MetadataAddress");
        exception.Message.ShouldContain("RequireHttpsMetadata");
    }

    [Fact]
    public void Should_ReturnOptions_When_HttpMetadataAddressIsUsedWithoutRequiringHttpsMetadata()
    {
        var configuration = Configure(new()
        {
            ["Keycloak:Enabled"] = "true",
            ["Keycloak:Authority"] = "https://keycloak.example/realms/blogdoft",
            ["Keycloak:MetadataAddress"] = "http://keycloak.internal.svc.cluster.local:8080/realms/blogdoft/.well-known/openid-configuration",
            ["Keycloak:RequireHttpsMetadata"] = "false",
        });

        var options = KeycloakOptions.FromConfiguration(configuration);

        options.ShouldNotBeNull();
        options.Authority.ShouldBe("https://keycloak.example/realms/blogdoft");
        options.MetadataAddress.ShouldBe("http://keycloak.internal.svc.cluster.local:8080/realms/blogdoft/.well-known/openid-configuration");
    }

    private static IConfiguration Configure(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
