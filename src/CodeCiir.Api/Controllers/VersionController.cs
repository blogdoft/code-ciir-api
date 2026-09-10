using CodeCiir.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CodeCiir.Api.Controllers;

/// <summary>
/// Reports the running API's own build version.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "Version")]
[Route("version")]
public sealed class VersionController : ControllerBase
{
    /// <summary>Get the API version</summary>
    /// <remarks>
    /// Returns the version this instance was built and published from. Deliberately
    /// unversioned (no /api/v1 prefix) and health-check-style, for use by deploy tooling and
    /// diagnostics rather than API consumers.
    /// </remarks>
    /// <response code="200">The running API's version.</response>
    [HttpGet]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK, "application/json")]
    public IActionResult Get() => Ok(new VersionResponse($"v{AppVersion.Current}"));
}
