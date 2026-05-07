using System.Diagnostics;
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

public interface IProvidersService
{
    IReadOnlyList<ProviderListing> List();
    ProviderListingDetail GetById(int id);
    void SetEnabled(int id, bool enabled);
    void SetSecrets(int id, IReadOnlyDictionary<string, string> values);
    Task<ProviderTestOutcome> TestAsync(int id, CancellationToken ct);
}

public sealed class ProvidersService(UserContext ctx, IFileSystem fs, ILogger<ProvidersService> logger) : IProvidersService
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
        var catalog = PortalCatalogLoader.Load(CatalogPath());
        var portal = catalog.FirstOrDefault(p => p.Id == id)
            ?? throw new NotFoundException($"provider id {id} not found");

        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        var disabled = state.Disabled.ToHashSet();
        if (enabled) disabled.Remove(id);
        else disabled.Add(id);

        var nextState = new ProviderState(disabled.OrderBy(i => i).ToArray(), state.Secrets);
        ProviderStateLoader.Save(ctx.ProviderStatePath, nextState);
    }

    public void SetSecrets(int id, IReadOnlyDictionary<string, string> values)
    {
        var catalog = PortalCatalogLoader.Load(CatalogPath());
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

        if (portal.Type == PortalType.Manual)
        {
            return new ProviderTestOutcome(
                Ok: false, FetchedCount: 0, DurationMs: 0,
                SampleTitle: null, Error: "manual provider — no live endpoint to test",
                TestedAt: DateTimeOffset.UtcNow);
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var adapter = AdapterFactory.Create(portal, http, NullLogger.Instance, ctx.ImportsDir, fs);
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
                Error: results.Count == 0 ? "fetch returned 0 listings" : null,
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
                Error: ex.Message,
                TestedAt: DateTimeOffset.UtcNow);
        }
    }

    private static string CatalogPath() => Path.Combine(AppContext.BaseDirectory, "portals.json");

    private (IReadOnlyList<PortalConfig> Catalog, ProviderState State) LoadCatalogAndState()
    {
        var catalog = PortalCatalogLoader.Load(CatalogPath());
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

    private Dictionary<string, LastFetch> LoadLastFetchByProvider()
    {
        var result = new Dictionary<string, LastFetch>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(ctx.HistoryDir)) return result;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(ctx.HistoryDir, "*.json")
                .OrderByDescending(p => p, StringComparer.Ordinal);
        }
        catch
        {
            return result;
        }

        foreach (var file in files)
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;

                if (!doc.RootElement.TryGetProperty("startedAt", out var startedProp)) continue;
                if (!doc.RootElement.TryGetProperty("providers", out var providersProp)) continue;
                if (providersProp.ValueKind != JsonValueKind.Array) continue;
                if (!startedProp.TryGetDateTimeOffset(out var startedAt)) continue;

                foreach (var prov in providersProp.EnumerateArray())
                {
                    if (prov.ValueKind != JsonValueKind.Object) continue;
                    if (!prov.TryGetProperty("name", out var nameProp)) continue;
                    var name = nameProp.GetString();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    int? count = null;
                    if (prov.TryGetProperty("fetchedCount", out var countProp) &&
                        countProp.ValueKind == JsonValueKind.Number)
                    {
                        count = countProp.GetInt32();
                    }

                    if (!result.ContainsKey(name))
                    {
                        result[name] = new LastFetch(startedAt, count);
                    }
                }
            }
            catch
            {
            }
        }

        return result;
    }

    private IReadOnlyList<ProviderRunHistory> LoadRecentRuns(string providerName, int take)
    {
        var result = new List<ProviderRunHistory>();
        if (!Directory.Exists(ctx.HistoryDir)) return result;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(ctx.HistoryDir, "*.json")
                .OrderByDescending(p => p, StringComparer.Ordinal);
        }
        catch
        {
            return result;
        }

        foreach (var file in files)
        {
            if (result.Count >= take) break;
            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;
                if (!doc.RootElement.TryGetProperty("runId", out var runIdProp)) continue;
                if (!doc.RootElement.TryGetProperty("startedAt", out var startedProp)) continue;
                if (!doc.RootElement.TryGetProperty("providers", out var providersProp)) continue;
                if (providersProp.ValueKind != JsonValueKind.Array) continue;
                if (!startedProp.TryGetDateTimeOffset(out var startedAt)) continue;

                foreach (var prov in providersProp.EnumerateArray())
                {
                    if (prov.ValueKind != JsonValueKind.Object) continue;
                    if (!prov.TryGetProperty("name", out var nameProp)) continue;
                    if (!string.Equals(nameProp.GetString(), providerName, StringComparison.OrdinalIgnoreCase)) continue;

                    var status = prov.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "unknown" : "unknown";
                    int? count = prov.TryGetProperty("fetchedCount", out var cProp) && cProp.ValueKind == JsonValueKind.Number
                        ? cProp.GetInt32()
                        : null;
                    string? error = prov.TryGetProperty("error", out var eProp) && eProp.ValueKind == JsonValueKind.String
                        ? eProp.GetString()
                        : null;

                    result.Add(new ProviderRunHistory(
                        RunId: runIdProp.GetString() ?? "",
                        StartedAt: startedAt,
                        Status: status,
                        FetchedCount: count,
                        Error: error));
                    break;
                }
            }
            catch
            {
            }
        }

        return result;
    }
}
