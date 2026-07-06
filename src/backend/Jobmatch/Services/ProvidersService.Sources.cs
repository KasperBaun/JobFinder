using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Services;

// User-added sources: detect a pasted URL, preview-test the candidate, persist it to the per-user
// store, and remove it again. Kept in its own partial so the core read/toggle service stays small.
public sealed partial class ProvidersService
{
    public IReadOnlyList<DetectedSource> Detect(string? url)
    {
        var candidates = detection.Detect(ParseUrl(url));
        if (candidates.Count == 0) return [];

        var existing = LoadCatalog();
        return candidates
            .Select(c => new DetectedSource(c.Kind, c.DisplayName, c.Summary, DuplicateWarning(c.Draft, existing)))
            .ToList();
    }

    public Task<ProviderTestOutcome> PreviewTestAsync(string? url, string kind, string? displayName, CancellationToken ct)
    {
        var candidate = SelectCandidate(url, kind, displayName);
        return TestConfigAsync(candidate.Draft, ct);
    }

    public ProviderListing Create(string? url, string kind, string? displayName)
    {
        var candidate = SelectCandidate(url, kind, displayName);
        var draft = candidate.Draft;
        if (!string.IsNullOrWhiteSpace(displayName))
            draft = draft with { DisplayName = displayName!.Trim() };

        var created = UserProviderStore.Add(ctx.UserProvidersPath, draft, LoadBakedCatalog());
        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        return MakeListing(created, state, LoadLastFetchByProvider());
    }

    public void Delete(int id)
    {
        if (id < UserProviderStore.IdBase)
            throw new InvalidRequestException("only sources you added yourself can be removed");
        if (!UserProviderStore.Remove(ctx.UserProvidersPath, id))
            throw new NotFoundException($"provider id {id} not found");
        RemoveFromState(id);
    }

    private SourceCandidate SelectCandidate(string? url, string kind, string? displayName)
    {
        if (kind == "manual")
            return detection.BuildManual(displayName ?? string.Empty);

        return detection.Detect(ParseUrl(url)).FirstOrDefault(c => c.Kind == kind)
            ?? throw new InvalidRequestException($"could not recognise a '{kind}' source at that address");
    }

    private void RemoveFromState(int id)
    {
        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        var secrets = state.Secrets.Where(kvp => kvp.Key != id)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var next = new ProviderState(
            state.Disabled.Where(x => x != id).ToArray(),
            state.Enabled.Where(x => x != id).ToArray(),
            secrets);
        ProviderStateLoader.Save(ctx.ProviderStatePath, next);
    }

    private static Uri ParseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidRequestException("a web address is required");
        var s = url.Trim();
        if (!s.Contains("://", StringComparison.Ordinal)) s = "https://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidRequestException($"'{url}' is not a valid web address");
        return uri;
    }

    private static string? DuplicateWarning(PortalConfig draft, IReadOnlyList<PortalConfig> existing)
    {
        if (draft.Endpoint is null) return null;
        var key = EndpointKey(draft.Endpoint);
        foreach (var p in existing)
        {
            if (p.Endpoint is null || p.Type != draft.Type) continue;
            if (EndpointKey(p.Endpoint) == key)
            {
                var name = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Name : p.DisplayName!;
                return $"You already pull from this source via “{name}”.";
            }
        }
        return null;
    }

    // Host + path identifies one board — ATS platforms share a host across every customer, so the
    // path must be part of the key or every board on that platform would falsely look like a dup.
    // The host alias collapses it-jobbank and jobindex, which serve the same feed from two hosts.
    private static string EndpointKey(Uri u) =>
        $"{BackendKey(u.Host)}{u.AbsolutePath.TrimEnd('/').ToLowerInvariant()}";

    private static string BackendKey(string host) => host.ToLowerInvariant() switch
    {
        "www.it-jobbank.dk" or "it-jobbank.dk" or "www.jobindex.dk" or "jobindex.dk" => "jobindex.dk",
        var h => h,
    };
}
