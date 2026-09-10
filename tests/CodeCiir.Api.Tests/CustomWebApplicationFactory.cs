using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CodeCiir.Api.Tests;

/// <summary>
/// Boots the API in-memory for HTTP end-to-end tests. No endpoint added so far touches the
/// database, so this deliberately does NOT point at a real Postgres instance - a syntactically
/// valid but unreachable connection string is enough to prove no test accidentally depends on
/// live infrastructure. Tests that do need a real database arrive in Fase 2 alongside the first
/// repository (see .specs/03-projects-endpoint.md) and will use Testcontainers instead.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Host=localhost;Port=1;Database=unused;Username=unused;Password=unused",
            });
        });
    }
}
