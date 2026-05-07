using System.Diagnostics;
using System.Text.Json;
using Jobmatch.Adapters;
using Jobmatch.Configuration;
using Jobmatch.Gui.Server.Models;
using Jobmatch.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Gui.Server.Handlers;

public static class ProvidersHandler
{
    private static IReadOnlyList<PortalConfig> LoadMerged(Jobmatch.UserContext ctx)
    {
        var catalog = PortalCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "portals.json"));
        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        return ProviderStateMerger.Merge(catalog, state);
    }

    private static (IReadOnlyList<PortalConfig> Catalog, ProviderState State) LoadCatalogAndState(Jobmatch.UserContext ctx)
    {
        var catalog = PortalCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "portals.json"));
        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        return (catalog, state);
    }

    public static IResult GetList(Jobmatch.UserContext ctx)
    {
        try
        {
            var (catalog, state) = LoadCatalogAndState(ctx);
            var lastByProvider = LoadLastFetchByProvider(ctx.HistoryDir);
            var summaries = catalog.Select(p => MakeSummary(p, state, lastByProvider)).ToList();
            return Results.Ok(new ProvidersResponse(summaries));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    public static IResult GetOne(int id, Jobmatch.UserContext ctx)
    {
        try
        {
            var (catalog, state) = LoadCatalogAndState(ctx);
            var match = catalog.FirstOrDefault(p => p.Id == id);
            if (match is null) return Results.NotFound();

            var lastByProvider = LoadLastFetchByProvider(ctx.HistoryDir);
            lastByProvider.TryGetValue(match.Name, out var last);
            var recent = LoadRecentRuns(ctx.HistoryDir, match.Name, take: 5);

            var enabled = ProviderStateMerger.IsUserEnabled(match, state);
            var hasSecret = ProviderStateMerger.HasSecretValue(match, state);

            return Results.Ok(new ProviderDetail(
                Id: match.Id,
                Name: match.Name,
                DisplayName: string.IsNullOrWhiteSpace(match.DisplayName) ? match.Name : match.DisplayName!,
                Type: match.Type.ToString().ToLowerInvariant(),
                Enabled: enabled,
                Endpoint: match.Endpoint?.ToString(),
                RateLimitRps: match.RateLimitRps,
                Notes: match.Notes,
                LastFetchedAt: last?.FetchedAt,
                LastFetchCount: last?.FetchedCount,
                RequiresSecret: match.RequiresSecret,
                HasSecret: hasSecret,
                RecentRuns: recent));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    // Only req.Enabled is read; all other fields are ignored — the catalog is read-only.
    public static IResult Update(int id, ProviderUpsert? req, Jobmatch.UserContext ctx)
    {
        if (req is null) return Results.BadRequest(new SaveResponse(false, "request body is required"));

        try
        {
            var catalog = PortalCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "portals.json"));
            var portal = catalog.FirstOrDefault(p => p.Id == id);
            if (portal is null) return Results.NotFound();

            var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
            var disabled = state.Disabled.ToHashSet();
            if (req.Enabled ?? true) disabled.Remove(id);
            else disabled.Add(id);

            var nextState = new ProviderState(disabled.OrderBy(i => i).ToArray(), state.Secrets);
            ProviderStateLoader.Save(ctx.ProviderStatePath, nextState);
            return Results.Ok(new SaveResponse(true));
        }
        catch (Exception ex)
        {
            GuiLog.Error($"PUT /api/providers/{id} — {ex.GetType().Name}: {ex.Message}");
            return Results.Ok(new SaveResponse(false, ex.Message));
        }
    }

    public static IResult SetSecrets(int id, SetSecretsRequest? req, Jobmatch.UserContext ctx)
    {
        if (req is null) return Results.BadRequest(new SaveResponse(false, "request body is required"));

        try
        {
            var catalog = PortalCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "portals.json"));
            var portal = catalog.FirstOrDefault(p => p.Id == id);
            if (portal is null) return Results.NotFound();
            if (portal.RequiresSecret is null)
                return Results.BadRequest(new SaveResponse(false, $"provider '{portal.Name}' does not declare requiresSecret"));

            var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
            var secrets = state.Secrets.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(kvp.Value));

            var current = secrets.TryGetValue(id, out var c)
                ? new Dictionary<string, string>(c)
                : new Dictionary<string, string>();
            foreach (var (k, v) in req.Values)
            {
                if (string.IsNullOrEmpty(v)) current.Remove(k);
                else current[k] = v;
            }
            if (current.Count == 0) secrets.Remove(id);
            else secrets[id] = current;

            var next = state with { Secrets = secrets };
            ProviderStateLoader.Save(ctx.ProviderStatePath, next);
            return Results.Ok(new SaveResponse(true));
        }
        catch (Exception ex)
        {
            GuiLog.Error($"PUT /api/providers/{id}/secrets — {ex.GetType().Name}: {ex.Message}");
            return Results.Ok(new SaveResponse(false, ex.Message));
        }
    }

    public static async Task<IResult> Test(int id, Jobmatch.UserContext ctx, CancellationToken ct)
    {
        try
        {
            var portals = LoadMerged(ctx);
            var portal = portals.FirstOrDefault(p => p.Id == id);
            if (portal is null) return Results.NotFound();

            if (portal.Type == PortalType.Manual)
            {
                return Results.Ok(new ProviderTestResult(
                    Ok: false, FetchedCount: 0, DurationMs: 0,
                    SampleTitle: null, Error: "manual provider — no live endpoint to test",
                    TestedAt: DateTimeOffset.UtcNow));
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var adapter = AdapterFactory.Create(portal, http, NullLogger.Instance, ctx.ImportsDir);
            if (adapter is null)
            {
                return Results.Ok(new ProviderTestResult(
                    Ok: false, FetchedCount: 0, DurationMs: 0,
                    SampleTitle: null, Error: $"unsupported portal type '{portal.Type}'",
                    TestedAt: DateTimeOffset.UtcNow));
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var results = await adapter.FetchAsync(ct).ConfigureAwait(false);
                sw.Stop();
                var sample = results.Count > 0 ? results[0].Title : null;
                return Results.Ok(new ProviderTestResult(
                    Ok: results.Count > 0,
                    FetchedCount: results.Count,
                    DurationMs: sw.ElapsedMilliseconds,
                    SampleTitle: sample,
                    Error: results.Count == 0 ? "fetch returned 0 listings" : null,
                    TestedAt: DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                sw.Stop();
                return Results.Ok(new ProviderTestResult(
                    Ok: false,
                    FetchedCount: 0,
                    DurationMs: sw.ElapsedMilliseconds,
                    SampleTitle: null,
                    Error: ex.Message,
                    TestedAt: DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static ProviderSummary MakeSummary(
        PortalConfig catalogPortal,
        ProviderState state,
        IReadOnlyDictionary<string, LastFetch> lastByProvider)
    {
        var enabled = ProviderStateMerger.IsUserEnabled(catalogPortal, state);
        var hasSecret = ProviderStateMerger.HasSecretValue(catalogPortal, state);
        lastByProvider.TryGetValue(catalogPortal.Name, out var last);
        return new ProviderSummary(
            Id: catalogPortal.Id,
            Name: catalogPortal.Name,
            DisplayName: string.IsNullOrWhiteSpace(catalogPortal.DisplayName) ? catalogPortal.Name : catalogPortal.DisplayName!,
            Type: catalogPortal.Type.ToString().ToLowerInvariant(),
            Enabled: enabled,
            Endpoint: catalogPortal.Endpoint?.ToString(),
            RateLimitRps: catalogPortal.RateLimitRps,
            Notes: catalogPortal.Notes,
            LastFetchedAt: last?.FetchedAt,
            LastFetchCount: last?.FetchedCount,
            RequiresSecret: catalogPortal.RequiresSecret,
            HasSecret: hasSecret);
    }

    private sealed record LastFetch(DateTimeOffset FetchedAt, int? FetchedCount);

    private static Dictionary<string, LastFetch> LoadLastFetchByProvider(string historyDir)
    {
        var result = new Dictionary<string, LastFetch>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(historyDir)) return result;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(historyDir, "*.json")
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

    private static IReadOnlyList<ProviderRecentRun> LoadRecentRuns(string historyDir, string providerName, int take)
    {
        var result = new List<ProviderRecentRun>();
        if (!Directory.Exists(historyDir)) return result;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(historyDir, "*.json")
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

                    result.Add(new ProviderRecentRun(
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

public sealed record CreateResponse(bool Success, int Id, string? Error = null);
