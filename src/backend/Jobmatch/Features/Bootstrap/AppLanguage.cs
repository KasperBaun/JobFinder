namespace Jobmatch.Features.Bootstrap;

/// <summary>
/// The interface languages the GUI ships catalogs for. Mirrors
/// <c>src/frontend/src/i18n/locale.ts</c> — adding one here without adding the catalog there (or the
/// reverse) is a bug, so keep the two lists in step.
/// </summary>
public static class AppLanguage
{
    public const string Default = "en";

    public static readonly IReadOnlySet<string> Supported =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "en", "da" };

    public static bool IsSupported(string? language) =>
        !string.IsNullOrWhiteSpace(language) && Supported.Contains(language.Trim());

    public static string? TryNormalize(string? language)
    {
        if (!IsSupported(language)) return null;
        return language!.Trim().ToLowerInvariant();
    }
}
