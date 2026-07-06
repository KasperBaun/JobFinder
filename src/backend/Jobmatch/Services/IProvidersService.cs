using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.IO;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Services;

public sealed record ProviderListing(
    PortalConfig Portal,
    bool Enabled,
    bool HasSecret,
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount);

public sealed record ProviderRunHistory(
    string RunId,
    DateTimeOffset StartedAt,
    string Status,
    int? FetchedCount,
    string? Error);

public sealed record ProviderListingDetail(
    ProviderListing Listing,
    IReadOnlyList<ProviderRunHistory> RecentRuns);

public sealed record ProviderTestOutcome(
    bool Ok,
    int FetchedCount,
    long DurationMs,
    string? SampleTitle,
    string? Error,
    DateTimeOffset TestedAt);

public sealed record DetectedSource(
    string Kind,
    string DisplayName,
    string Summary,
    string? DuplicateWarning);

public interface IProvidersService
{
    IReadOnlyList<ProviderListing> List();
    ProviderListingDetail GetById(int id);
    void SetEnabled(int id, bool enabled);
    void SetSecrets(int id, IReadOnlyDictionary<string, string> values);
    Task<ProviderTestOutcome> TestAsync(int id, CancellationToken ct);
    IReadOnlyList<DetectedSource> Detect(string? url);
    Task<ProviderTestOutcome> PreviewTestAsync(string? url, string kind, string? displayName, CancellationToken ct);
    ProviderListing Create(string? url, string kind, string? displayName);
    void Delete(int id);
}

public sealed partial class ProvidersService(
    UserContext ctx,
    IFileSystem fs,
    ISourceDetectionService detection,
    ILogger<ProvidersService> logger) : IProvidersService
{
    public IReadOnlyList<ProviderListing> List()
    {
        var (catalog, state) = LoadCatalogAndState();
        var lastByProvider = LoadLastFetchByProvider();
        return catalog.Select(p => MakeListing(p, state, lastByProvider)).ToList();
    }

    public ProviderListingDetail GetById(int id)
    {
        var (catalog, state) = LoadCatalogAndState();
        var portal = catalog.FirstOrDefault(p => p.Id == id)
            ?? throw new NotFoundException($"provider id {id} not found");

        var lastByProvider = LoadLastFetchByProvider();
        var listing = MakeListing(portal, state, lastByProvider);
        var recent = LoadRecentRuns(portal.Name, take: 5);
        return new ProviderListingDetail(listing, recent);
    }

    public void SetEnabled(int id, bool enabled)
    {
        var catalog = LoadCatalog();
        var portal = catalog.FirstOrDefault(p => p.Id == id)
            ?? throw new NotFoundException($"provider id {id} not found");

        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        var disabled = state.Disabled.ToHashSet();
        var explicitEnabled = state.Enabled.ToHashSet();

        // Symmetric overrides — the catalog default decides which list to flip:
        //   catalog enabled=true  → toggle on  = clear opt-out;        toggle off = add opt-out
        //   catalog enabled=false → toggle on  = add explicit opt-in;  toggle off = clear opt-in
        if (portal.Enabled)
        {
            explicitEnabled.Remove(id);
            if (enabled) disabled.Remove(id);
            else disabled.Add(id);
        }
        else
        {
            disabled.Remove(id);
            if (enabled) explicitEnabled.Add(id);
            else explicitEnabled.Remove(id);
        }

        var nextState = new ProviderState(
            disabled.OrderBy(i => i).ToArray(),
            explicitEnabled.OrderBy(i => i).ToArray(),
            state.Secrets);
        ProviderStateLoader.Save(ctx.ProviderStatePath, nextState);
    }

    public void SetSecrets(int id, IReadOnlyDictionary<string, string> values)
    {
        var catalog = LoadCatalog();
        var portal = catalog.FirstOrDefault(p => p.Id == id)
            ?? throw new NotFoundException($"provider id {id} not found");

        if (portal.RequiresSecret is null)
            throw new InvalidRequestException($"provider '{portal.Name}' does not declare requiresSecret");

        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        var secrets = state.Secrets.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(kvp.Value));

        var current = secrets.TryGetValue(id, out var c)
            ? new Dictionary<string, string>(c)
            : new Dictionary<string, string>();
        foreach (var (k, v) in values)
        {
            if (string.IsNullOrEmpty(v)) current.Remove(k);
            else current[k] = v;
        }
        if (current.Count == 0) secrets.Remove(id);
        else secrets[id] = current;

        var next = state with { Secrets = secrets };
        ProviderStateLoader.Save(ctx.ProviderStatePath, next);
    }

    public async Task<ProviderTestOutcome> TestAsync(int id, CancellationToken ct)
    {
        var portals = LoadMerged();
        var portal = portals.FirstOrDefault(p => p.Id == id)
            ?? throw new NotFoundException($"provider id {id} not found");
        return await TestConfigAsync(portal, ct).ConfigureAwait(false);
    }

    private async Task<ProviderTestOutcome> TestConfigAsync(PortalConfig portal, CancellationToken ct)
    {
        if (portal.Type == PortalType.Manual)
        {
            return new ProviderTestOutcome(
                Ok: false, FetchedCount: 0, DurationMs: 0,
                SampleTitle: null, Error: "manual provider — no live endpoint to test",
                TestedAt: DateTimeOffset.UtcNow);
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var adapter = AdapterFactory.Create(ForConnectivityTest(portal), http, NullLogger.Instance, ctx.ImportsDir, fs);
        if (adapter is null)
        {
            return new ProviderTestOutcome(
                Ok: false, FetchedCount: 0, DurationMs: 0,
                SampleTitle: null, Error: $"unsupported portal type '{portal.Type}'",
                TestedAt: DateTimeOffset.UtcNow);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var results = await adapter.FetchAsync(ct).ConfigureAwait(false);
            sw.Stop();
            var sample = results.Count > 0 ? results[0].Title : null;
            return new ProviderTestOutcome(
                Ok: results.Count > 0,
                FetchedCount: results.Count,
                DurationMs: sw.ElapsedMilliseconds,
                SampleTitle: sample,
                Error: results.Count == 0 ? "This source is reachable but returned no jobs right now." : null,
                TestedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "Provider test failed for {ProviderName}", portal.Name);
            return new ProviderTestOutcome(
                Ok: false,
                FetchedCount: 0,
                DurationMs: sw.ElapsedMilliseconds,
                SampleTitle: null,
                Error: FriendlyError(ex),
                TestedAt: DateTimeOffset.UtcNow);
        }
    }

    // A connectivity test is list-only: it never enriches bodies. Body enrichment fetches every
    // listing's detail page sequentially (~5 rps, uncapped), turning a one-request "does this work?"
    // check into a full N-request crawl of a third-party site. The test signal (count + one title)
    // comes from the list response, so enrichment adds latency and load without adding information.
    // (TeamTailor is inherently page-per-job — its listings only exist behind detail pages — so this
    // does not lighten a TeamTailor test; every other enrich-capable adapter honours the flag.)
    internal static PortalConfig ForConnectivityTest(PortalConfig portal) =>
        portal with { EnrichBody = false };

    // Fetch failures surface as raw framework exception text ("Response status code does not indicate
    // success: 404 (Not Found).") which means nothing to a non-technical user. Map the common cases
    // to plain language; the full exception is still logged above for diagnosis.
    internal static string FriendlyError(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
            "This source no longer exists at that address (404). The board may have moved or closed.",
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
            "This source refused access — it may need an access key or block automated requests.",
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
            "This source is rate-limiting requests right now — try again in a little while.",
        HttpRequestException { StatusCode: { } code } =>
            $"This source responded with an error ({(int)code}).",
        HttpRequestException =>
            "Couldn't reach this source — check the web address or your internet connection.",
        TaskCanceledException or TimeoutException =>
            "This source took too long to respond (timed out).",
        _ => ex.Message,
    };

    private static string CatalogPath() => Path.Combine(AppContext.BaseDirectory, "portals.json");

    private static IReadOnlyList<PortalConfig> LoadBakedCatalog() => PortalCatalogLoader.Load(CatalogPath());

    // The effective catalog is the shipped one plus the user's own added sources.
    private IReadOnlyList<PortalConfig> LoadCatalog()
    {
        var baked = LoadBakedCatalog();
        var user = UserProviderStore.Load(ctx.UserProvidersPath);
        return user.Count == 0 ? baked : [.. baked, .. user];
    }

    private (IReadOnlyList<PortalConfig> Catalog, ProviderState State) LoadCatalogAndState()
    {
        var catalog = LoadCatalog();
        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        return (catalog, state);
    }

    private IReadOnlyList<PortalConfig> LoadMerged()
    {
        var (catalog, state) = LoadCatalogAndState();
        return ProviderStateMerger.Merge(catalog, state);
    }

    private static ProviderListing MakeListing(
        PortalConfig portal,
        ProviderState state,
        IReadOnlyDictionary<string, LastFetch> lastByProvider)
    {
        var enabled = ProviderStateMerger.IsUserEnabled(portal, state);
        var hasSecret = ProviderStateMerger.HasSecretValue(portal, state);
        lastByProvider.TryGetValue(portal.Name, out var last);
        return new ProviderListing(
            Portal: portal,
            Enabled: enabled,
            HasSecret: hasSecret,
            LastFetchedAt: last?.FetchedAt,
            LastFetchCount: last?.FetchedCount);
    }

    private sealed record LastFetch(DateTimeOffset FetchedAt, int? FetchedCount);
}
