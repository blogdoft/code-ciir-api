using System.Security.Claims;

namespace CodeCiir.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Best human-readable identity of the caller for log lines: Keycloak's <c>preferred_username</c>,
    /// falling back to <c>name</c>, then <c>sub</c>, then <c>"anonymous"</c>. Inbound claim mapping is
    /// off (see <c>AddKeycloakAuthentication</c>), so claims keep their raw JWT names.
    /// </summary>
    /// <param name="principal">The caller, possibly <see langword="null"/>.</param>
    /// <returns>The caller's username for logging.</returns>
    public static string GetUsername(this ClaimsPrincipal? principal) =>
        principal?.FindFirst("preferred_username")?.Value
        ?? principal?.FindFirst("name")?.Value
        ?? principal?.FindFirst("sub")?.Value
        ?? "anonymous";
}
