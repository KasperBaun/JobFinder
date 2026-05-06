using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Adapters;

public abstract class BaseAdapter(PortalConfig config, HttpClient http, ILogger logger) : IJobPortalAdapter
{
    protected PortalConfig Config { get; } = config;
    protected HttpClient Http { get; } = http;
    protected ILogger Logger { get; } = logger;

    public string PortalName => Config.Name;

    public abstract Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default);

    private DateTimeOffset _lastCallAt = DateTimeOffset.MinValue;

    protected async Task ThrottleAsync(CancellationToken ct)
    {
        var rps = Config.RateLimitRps;
        if (rps <= 0) return;
        var minIntervalMs = 1000.0 / rps;
        var elapsed = (DateTimeOffset.UtcNow - _lastCallAt).TotalMilliseconds;
        if (elapsed < minIntervalMs)
        {
            await Task.Delay((int)Math.Ceiling(minIntervalMs - elapsed), ct);
        }
        _lastCallAt = DateTimeOffset.UtcNow;
    }

    protected static string StableId(string portal, string sourceOrUrl)
    {
        var input = $"{portal}:{sourceOrUrl}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(32);
        foreach (var b in bytes.AsSpan(0, 16)) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    protected static string StripHtml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new StringBuilder(input.Length);
        var inside = false;
        foreach (var ch in input)
        {
            if (ch == '<') inside = true;
            else if (ch == '>') inside = false;
            else if (!inside) sb.Append(ch);
        }
        return System.Net.WebUtility.HtmlDecode(sb.ToString()).Trim();
    }

    protected static RemoteMode InferRemoteMode(string text)
    {
        if (string.IsNullOrEmpty(text)) return RemoteMode.Unknown;
        var lower = text.ToLowerInvariant();
        if (lower.Contains("hybrid")) return RemoteMode.Hybrid;
        if (lower.Contains("fully remote") || lower.Contains("100% remote") || lower.Contains("remote-only") || lower.Contains("remote only") || lower.Contains("work from home") || lower.Contains("wfh")) return RemoteMode.Remote;
        if (lower.Contains("onsite") || lower.Contains("on-site") || lower.Contains("in-office")) return RemoteMode.Onsite;
        if (lower.Contains("remote")) return RemoteMode.Remote;
        return RemoteMode.Unknown;
    }

    protected Listing BuildListing(
        string sourceId,
        string title,
        string? company,
        string? location,
        string description,
        Uri url,
        DateTimeOffset? postedAt,
        JsonElement raw)
    {
        if (Config.StaticFields is { } sf)
        {
            if (sf.TryGetValue("title", out var t) && !string.IsNullOrWhiteSpace(t)) title = t;
            if (sf.TryGetValue("company", out var c) && !string.IsNullOrWhiteSpace(c)) company = c;
            if (sf.TryGetValue("location", out var l) && !string.IsNullOrWhiteSpace(l)) location = l;
        }

        return new Listing(
            Id: StableId(Config.Name, sourceId),
            Portal: Config.Name,
            Title: title,
            Company: company,
            Location: location,
            RemoteMode: InferRemoteMode($"{title} {description} {location}"),
            Description: description,
            Url: url,
            PostedAt: postedAt,
            FetchedAt: DateTimeOffset.UtcNow,
            Raw: raw);
    }
}
