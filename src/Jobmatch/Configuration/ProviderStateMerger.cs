namespace Jobmatch.Configuration;

using Jobmatch.Models;

public static class ProviderStateMerger
{
    public static IReadOnlyList<PortalConfig> Merge(
        IReadOnlyList<PortalConfig> catalog,
        ProviderState state)
    {
        var disabled = new HashSet<int>(state.Disabled);
        var result = new List<PortalConfig>(catalog.Count);
        foreach (var p in catalog)
        {
            var hasSecretValue = false;
            IReadOnlyDictionary<string, string>? secrets = null;
            if (state.Secrets.TryGetValue(p.Id, out var s) && s is not null)
            {
                secrets = s;
                if (p.RequiresSecret is not null
                    && s.TryGetValue(p.RequiresSecret, out var v)
                    && !string.IsNullOrEmpty(v))
                {
                    hasSecretValue = true;
                }
            }

            var effectiveEnabled = p.Enabled
                && !disabled.Contains(p.Id)
                && (p.RequiresSecret is null || hasSecretValue);

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
