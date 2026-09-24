using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace CodeCiir.Api.Tests.Support;

internal static class HttpContextFactory
{
    /// <summary>An <see cref="HttpContext"/> whose services log into <paramref name="logs"/>, optionally authenticated as <paramref name="claims"/>.</summary>
    /// <param name="logs">Receives everything the code under test logs.</param>
    /// <param name="method">HTTP method of the fake request.</param>
    /// <param name="path">Path of the fake request.</param>
    /// <param name="claims">When given, the request's user is authenticated with exactly these claims.</param>
    /// <returns>The fake HTTP context.</returns>
    public static DefaultHttpContext Create(CapturingLoggerProvider logs, string method = "GET", string path = "/api/test", params Claim[] claims)
    {
        var services = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(logs))
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = method;
        context.Request.Path = path;

        if (claims.Length > 0)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        return context;
    }
}
