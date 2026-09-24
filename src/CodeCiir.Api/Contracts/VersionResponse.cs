namespace CodeCiir.Api.Contracts;

/// <summary>The running API's own build version. Serializes as camelCase.</summary>
/// <param name="Version">
/// Semantic version stamped at publish time (see .specs/08-ops-deployment.md), or
/// "0.0.0-dev" for a local build that wasn't given an explicit version.
/// </param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record VersionResponse(string Version);
#pragma warning restore SA1313
