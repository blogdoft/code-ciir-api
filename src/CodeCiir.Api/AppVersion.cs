using System.Reflection;

namespace CodeCiir.Api;

/// <summary>This API's own build version, embedded at publish time via the Version MSBuild property.</summary>
internal static class AppVersion
{
    public static string Current { get; } = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion
        ?? "0.0.0-dev";
}
