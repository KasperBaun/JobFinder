using Jobmatch.Models;

namespace Jobmatch.Configuration;

/// <summary>
/// Cross-record validation rules for a list of <see cref="PortalConfig"/> records.
/// Disjunctive rules ("name OR endpoint must not collide") cannot be expressed as a single
/// <c>IEqualityComparer</c>, so this is a list-level guard rather than a comparer.
/// </summary>
public static class ProviderListValidator
{
    public static void AssertNoDuplicates(IReadOnlyList<PortalConfig> portals)
    {
        var seenNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var seenEndpoints = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var p in portals)
        {
            var nameKey = NormalizeName(p.Name);
            if (seenNames.TryGetValue(nameKey, out var prior))
            {
                throw new ConfigException(
                    $"duplicate provider name: '{p.Name}' collides with '{prior}' (names are case-insensitive after trimming)");
            }
            seenNames[nameKey] = p.Name;

            if (p.Endpoint is null) continue;
            var endpointKey = $"{p.Type}|{NormalizeEndpoint(p.Endpoint)}";
            if (seenEndpoints.TryGetValue(endpointKey, out var priorName))
            {
                throw new ConfigException(
                    $"duplicate provider endpoint: '{p.Name}' and '{priorName}' both target {NormalizeEndpoint(p.Endpoint)} as {p.Type}");
            }
            seenEndpoints[endpointKey] = p.Name;
        }
    }

    public static string NormalizeName(string name) =>
        (name ?? string.Empty).Trim().ToLowerInvariant();

    public static string NormalizeEndpoint(Uri endpoint)
    {
        var builder = new UriBuilder(endpoint)
        {
            Scheme = endpoint.Scheme.ToLowerInvariant(),
            Host = endpoint.Host.ToLowerInvariant(),
            Fragment = string.Empty,
        };
        if (endpoint.IsDefaultPort) builder.Port = -1;
        return builder.Uri.AbsoluteUri;
    }
}
