using System.Globalization;
using System.Text.RegularExpressions;
using Jobmatch.Models;

namespace Jobmatch.Services;

/// <summary>
/// One recognised way to add a source. <see cref="Draft"/> is the server-built provider config and
/// is never sent to the client verbatim — the API projects only <see cref="Kind"/>,
/// <see cref="DisplayName"/> and <see cref="Summary"/>. Create/preview re-run detection and select
/// by <see cref="Kind"/>, so the client never hands the server a raw endpoint or field mapping.
/// </summary>
public sealed record SourceCandidate(
    string Kind,
    string DisplayName,
    string Summary,
    PortalConfig Draft);

public interface ISourceDetectionService
{
    /// <summary>Pattern-matches a pasted URL to known ATS boards or an RSS feed. Pure; no network.</summary>
    IReadOnlyList<SourceCandidate> Detect(Uri url);

    /// <summary>Builds a manual-import source from a user-supplied name (no endpoint).</summary>
    SourceCandidate BuildManual(string displayName);
}

/// <summary>
/// Recognises the common cases a non-technical user can add by pasting a URL — job boards on the
/// major ATS platforms (Greenhouse, Ashby, Lever, SmartRecruiters, Teamtailor, HR-Manager) plus
/// generic RSS feeds. The generated config mirrors a proven catalog entry for each platform, so the
/// mapping is known-good rather than authored on the fly. Anything unrecognised yields no candidate,
/// and the caller falls back to manual import.
/// </summary>
public sealed class SourceDetectionService : ISourceDetectionService
{
    public IReadOnlyList<SourceCandidate> Detect(Uri url)
    {
        var host = url.Host.ToLowerInvariant();
        var segments = url.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        var candidate =
            MatchGreenhouse(host, segments) ??
            MatchAshby(host, segments) ??
            MatchLever(host, segments) ??
            MatchSmartRecruiters(host, segments) ??
            MatchTeamtailor(host) ??
            MatchHrManager(host, url) ??
            MatchRss(url);

        return candidate is null ? [] : [candidate];
    }

    public SourceCandidate BuildManual(string displayName)
    {
        var name = Slugify(displayName is { Length: > 0 } ? displayName : "manual-source");
        var pretty = displayName is { Length: > 0 } ? displayName : "Manual source";
        var draft = new PortalConfig(
            Name: $"manual-{name}",
            Type: PortalType.Manual,
            Enabled: true,
            DisplayName: pretty,
            Notes: $"Manual import. Save your saved roles as a CSV in your imports folder named "
                 + $"manual-{name}-*.csv (columns: url, title; optional company, location, description, posted_at). "
                 + $"They're picked up on the next search run.");
        return new SourceCandidate("manual", pretty, "Import a spreadsheet you export yourself.", draft);
    }

    private static SourceCandidate? MatchGreenhouse(string host, string[] segments)
    {
        if (host is not ("boards.greenhouse.io" or "job-boards.greenhouse.io") || segments.Length == 0)
            return null;
        var slug = segments[0];
        var draft = ApiDraft(
            name: $"greenhouse-{slug.ToLowerInvariant()}",
            display: Prettify(slug),
            endpoint: $"https://boards-api.greenhouse.io/v1/boards/{slug}/jobs",
            query: new() { ["content"] = "true" },
            mapping: new()
            {
                ["items_path"] = "jobs", ["id"] = "id", ["title"] = "title",
                ["location"] = "location.name", ["description"] = "content",
                ["url"] = "absolute_url", ["posted_at"] = "updated_at",
            });
        return new SourceCandidate("greenhouse", draft.DisplayName!,
            $"Greenhouse job board for {draft.DisplayName} — fetched automatically.", draft);
    }

    private static SourceCandidate? MatchAshby(string host, string[] segments)
    {
        if (host != "jobs.ashbyhq.com" || segments.Length == 0) return null;
        var slug = segments[0];
        var draft = ApiDraft(
            name: $"ashby-{slug.ToLowerInvariant()}",
            display: Prettify(slug),
            endpoint: $"https://api.ashbyhq.com/posting-api/job-board/{slug}",
            query: null,
            mapping: new()
            {
                ["items_path"] = "jobs", ["id"] = "id", ["title"] = "title",
                ["location"] = "location", ["description"] = "descriptionHtml",
                ["url"] = "jobUrl", ["posted_at"] = "publishedAt",
            });
        return new SourceCandidate("ashby", draft.DisplayName!,
            $"Ashby job board for {draft.DisplayName} — fetched automatically.", draft);
    }

    private static SourceCandidate? MatchLever(string host, string[] segments)
    {
        if (host != "jobs.lever.co" || segments.Length == 0) return null;
        var slug = segments[0];
        var draft = ApiDraft(
            name: $"lever-{slug.ToLowerInvariant()}",
            display: Prettify(slug),
            endpoint: $"https://api.lever.co/v0/postings/{slug}",
            query: new() { ["mode"] = "json" },
            mapping: new()
            {
                ["id"] = "id", ["title"] = "text", ["location"] = "categories.location",
                ["description"] = "descriptionPlain", ["url"] = "hostedUrl",
            });
        return new SourceCandidate("lever", draft.DisplayName!,
            $"Lever job board for {draft.DisplayName} — fetched automatically.", draft);
    }

    private static SourceCandidate? MatchSmartRecruiters(string host, string[] segments)
    {
        if (host is not ("jobs.smartrecruiters.com" or "careers.smartrecruiters.com") || segments.Length == 0)
            return null;
        var slug = segments[0];
        var draft = ApiDraft(
            name: $"smartrecruiters-{slug.ToLowerInvariant()}",
            display: Prettify(slug),
            endpoint: $"https://api.smartrecruiters.com/v1/companies/{slug}/postings",
            query: new() { ["country"] = "dk", ["limit"] = "100" },
            mapping: new()
            {
                ["items_path"] = "content", ["id"] = "id", ["title"] = "name",
                ["location"] = "location.fullLocation",
                ["url_template"] = $"https://jobs.smartrecruiters.com/{slug}/{{id}}",
                ["posted_at"] = "releasedDate",
            },
            enrichBody: true);
        return new SourceCandidate("smartrecruiters", draft.DisplayName!,
            $"SmartRecruiters job board for {draft.DisplayName} (Denmark) — fetched automatically.", draft);
    }

    private static SourceCandidate? MatchTeamtailor(string host)
    {
        if (!host.EndsWith(".teamtailor.com", StringComparison.Ordinal)) return null;
        var sub = host[..^".teamtailor.com".Length];
        if (sub is "" or "www") return null;
        var display = Prettify(sub);
        var draft = new PortalConfig(
            Name: $"teamtailor-{sub}",
            Type: PortalType.TeamTailor,
            Enabled: true,
            Endpoint: new Uri($"https://{host}/sitemap.xml"),
            DisplayName: display,
            StaticFields: new Dictionary<string, string> { ["company"] = display },
            EnrichBody: true);
        return new SourceCandidate("teamtailor", display,
            $"Teamtailor career site for {display} — fetched automatically.", draft);
    }

    private static SourceCandidate? MatchHrManager(string host, Uri url)
    {
        if (host != "candidate.hr-manager.net"
            || !url.AbsolutePath.Contains("list.aspx", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(url.Query))
            return null;
        var key = HrManagerKey(url.Query);
        var display = key is null ? "HR-Manager source" : Prettify(key);
        var draft = new PortalConfig(
            Name: $"hr-manager-{(key ?? "source").ToLowerInvariant()}",
            Type: PortalType.HrManager,
            Enabled: true,
            Endpoint: url,
            DisplayName: display,
            EnrichBody: true);
        return new SourceCandidate("hrmanager", display,
            $"HR-Manager.net vacancy list — fetched automatically.", draft);
    }

    private static SourceCandidate? MatchRss(Uri url)
    {
        var path = url.AbsolutePath.ToLowerInvariant();
        var looksLikeFeed =
            path.EndsWith(".rss", StringComparison.Ordinal) ||
            path.EndsWith(".atom", StringComparison.Ordinal) ||
            path.EndsWith(".xml", StringComparison.Ordinal) ||
            path.Contains("/rss", StringComparison.Ordinal) ||
            path.Contains("/feed", StringComparison.Ordinal);
        if (!looksLikeFeed) return null;

        var display = Prettify(url.Host.Replace("www.", "", StringComparison.Ordinal).Split('.')[0]);
        var draft = new PortalConfig(
            Name: $"rss-{Slugify(url.Host)}",
            Type: PortalType.Rss,
            Enabled: true,
            Endpoint: url,
            DisplayName: $"{display} (feed)",
            EnrichBody: true);
        return new SourceCandidate("rss", draft.DisplayName!,
            "Looks like a job feed — fetched automatically.", draft);
    }

    private static PortalConfig ApiDraft(
        string name,
        string display,
        string endpoint,
        Dictionary<string, object?>? query,
        Dictionary<string, string> mapping,
        bool enrichBody = false) =>
        new(
            Name: name,
            Type: PortalType.Api,
            Enabled: true,
            Endpoint: new Uri(endpoint),
            QueryParams: query,
            ResponseMapping: mapping,
            StaticFields: new Dictionary<string, string> { ["company"] = display },
            DisplayName: display,
            EnrichBody: enrichBody);

    private static string? HrManagerKey(string query)
    {
        var q = query.TrimStart('?');
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0] is "cid" or "customer" && !string.IsNullOrWhiteSpace(kv[1]))
                return kv[1];
        }
        return null;
    }

    private static string Prettify(string slug)
    {
        var s = Regex.Replace(slug.Replace('-', ' ').Replace('_', ' '), @"\d+$", "").Trim();
        if (s.Length == 0) s = slug;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);
    }

    private static string Slugify(string s) =>
        Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
}
