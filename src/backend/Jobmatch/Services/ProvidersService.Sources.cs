using Jobmatch.Configuration;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Services;

// User-added sources: resolve a pasted URL to a candidate, preview-fetch it, persist it to the
// per-user store, and remove it again. Kept in its own partial so the core read/toggle service stays
// small; the "do you already have this?" comparison lives in ProvidersService.Overlap.cs.
public sealed partial class ProvidersService
{
    public async Task<IReadOnlyList<DetectedSource>> DetectAsync(string? url, CancellationToken ct)
    {
        var candidates = await ResolveCandidatesAsync(ParseUrl(url), ct).ConfigureAwait(false);
        return [.. candidates.Select(c => new DetectedSource(c.Kind, c.DisplayName, c.Summary))];
    }

    public async Task<SourcePreview> PreviewAsync(string? url, string kind, string? displayName, CancellationToken ct)
    {
        var candidate = await SelectCandidateAsync(url, kind, displayName, ct).ConfigureAwait(false);
        var test = await TestConfigAsync(candidate.Draft, ct).ConfigureAwait(false);
        // The overlap probe fetches other sources, so it only earns its cost once this one works.
        var overlap = test.Ok
            ? await FindOverlapAsync(candidate.Draft, test.Samples, ct).ConfigureAwait(false)
            : null;
        return new SourcePreview(test, overlap);
    }

    public async Task<ProviderListing> CreateAsync(string? url, string kind, string? displayName, CancellationToken ct)
    {
        var candidate = await SelectCandidateAsync(url, kind, displayName, ct).ConfigureAwait(false);
        var draft = candidate.Draft;
        // The name the user typed is the best company name anyone has for this board — it has to
        // reach the listings too, not just the card, or every job arrives labelled with the platform
        // default (or nothing) while the source itself reads correctly.
        if (!string.IsNullOrWhiteSpace(displayName))
            draft = SourceDetectionService.WithBrand(candidate, displayName).Draft with { Name = draft.Name };

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

    // Pattern matching first because it is free and exact; only a URL nothing recognises is worth a
    // request to a stranger's server.
    private async Task<IReadOnlyList<SourceCandidate>> ResolveCandidatesAsync(Uri url, CancellationToken ct)
    {
        var matched = detection.Detect(url);
        if (matched.Count > 0) return matched;

        try
        {
            return await discovery.DiscoverAsync(url, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Link discovery failed for {Url}", url);
            return [];
        }
    }

    private async Task<SourceCandidate> SelectCandidateAsync(
        string? url, string kind, string? displayName, CancellationToken ct)
    {
        if (kind == "manual")
            return detection.BuildManual(displayName ?? string.Empty);

        var candidates = await ResolveCandidatesAsync(ParseUrl(url), ct).ConfigureAwait(false);
        return candidates.FirstOrDefault(c => c.Kind == kind)
            ?? throw new InvalidRequestException($"could not recognise a '{kind}' source at that address");
    }

    private void RemoveFromState(int id)
    {
        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        var secrets = state.Secrets.Where(kvp => kvp.Key != id)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var overrides = state.Overrides.Where(kvp => kvp.Key != id)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var next = state with
        {
            Disabled = state.Disabled.Where(x => x != id).ToArray(),
            Enabled = state.Enabled.Where(x => x != id).ToArray(),
            Secrets = secrets,
            Overrides = overrides,
        };
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

}
