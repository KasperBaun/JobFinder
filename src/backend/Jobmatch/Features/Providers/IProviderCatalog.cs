namespace Jobmatch.Features.Providers;

/// <summary>
/// The single answer to "what sources does this user have?". Everything that needs a provider list —
/// the providers page, a connectivity test, a search run — asks here, so no caller can end up with a
/// different set than another.
/// </summary>
public interface IProviderCatalog
{
    /// <summary>The shipped catalog only. Used when a new user source needs checking for collisions
    /// against entries the app owns.</summary>
    IReadOnlyList<PortalConfig> Shipped();

    /// <summary>The shipped catalog plus the sources this user added themselves, as declared —
    /// no per-user enable/disable, secrets or overrides applied.</summary>
    IReadOnlyList<PortalConfig> All();

    /// <summary>
    /// <see cref="All"/> with the user's state layered on: enabled/disabled resolved, secrets
    /// substituted, per-source overrides applied. This is what a search run fetches from.
    /// </summary>
    IReadOnlyList<PortalConfig> Effective();

    /// <summary>The raw per-user state, for callers that need the opt-in/opt-out lists themselves.</summary>
    ProviderState State();
}
