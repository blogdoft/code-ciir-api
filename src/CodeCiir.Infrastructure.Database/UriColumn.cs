namespace CodeCiir.Infrastructure.Database;

/// <summary>
/// Maps nullable text columns to <see cref="Uri"/>. code-ciir-indexer owns these columns and may
/// leave them as an empty string instead of NULL, which <see cref="Uri"/> would reject.
/// </summary>
internal static class UriColumn
{
    public static Uri? ToUriOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.Absolute);
}
