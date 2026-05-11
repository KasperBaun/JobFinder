namespace Jobmatch.Configuration;

using Jobmatch.Models;

public static class ProviderStateMerger
{
    public static bool IsUserEnabled(PortalConfig catalogPortal, ProviderState state)
    {
        // Explicit user opt-in always wins over a catalog-disabled default.
        // Otherwise: catalog default applies, with the user's opt-out list removing entries.
        if (state.Enabled.Contains(catalogPortal.Id)) return true;
        return catalogPortal.Enabled && !state.Disabled.Contains(catalogPortal.Id);
    }

    public static bool HasSecretValue(PortalConfig catalogPortal, ProviderState state)
    {
        if (catalogPortal.RequiresSecret is null) return false;
        if (!state.Secrets.TryGetValue(catalogPortal.Id, out var secrets)) return false;
        return secrets.TryGetValue(catalogPortal.RequiresSecret, out var v) && !string.IsNullOrEmpty(v);
    }

    public static IReadOnlyList<PortalConfig> Merge(
        IReadOnlyList<PortalConfig> catalog,
        ProviderState state)
    {
        var result = new List<PortalConfig>(catalog.Count);
        foreach (var p in catalog)
        {
            IReadOnlyDictionary<string, string>? secrets = null;
            if (state.Secrets.TryGetValue(p.Id, out var s) && s is not null)
                secrets = s;

            var effectiveEnabled = IsUserEnabled(p, state)
                && (p.RequiresSecret is null || HasSecretValue(p, state));

            var resolvedQuery = SubstituteSecrets(p.QueryParams, secrets);
            var resolvedBody = SubstituteSecrets(p.BodyTemplate, secrets);

            result.Add(p with
            {
                Enabled = effectiveEnabled,
                QueryParams = resolvedQuery,
                BodyTemplate = resolvedBody,
            });
        }
        return result;
    }

    private static IReadOnlyDictionary<string, object?>? SubstituteSecrets(
        IReadOnlyDictionary<string, object?>? source,
        IReadOnlyDictionary<string, string>? secrets)
    {
        if (source is null || secrets is null) return source;
        var copy = new Dictionary<string, object?>(source);
        foreach (var (k, v) in secrets)
        {
            if (copy.ContainsKey(k)) copy[k] = v;
        }
        return copy;
    }
}
