using Jobmatch.Models;

namespace Jobmatch.Services;

// One matcher per recognised platform. Each returns a draft that mirrors a proven catalog entry for
// that platform, so a user-added board is fetched by the same known-good mapping as a shipped one.
public sealed partial class SourceDetectionService
{
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
            },
            company: Prettify(slug));
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
            },
            company: Prettify(slug));
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
            },
            company: Prettify(slug));
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
            // SmartRecruiters caps the postings list at 100/page and pages via offset; without
            // this a company with >100 DK roles would silently return only the first 100.
            pagination: new PaginationConfig(Param: "offset", Start: 0, Step: 100, SizeParam: "limit", Size: 100, MaxPages: 10),
            enrichBody: true,
            company: Prettify(slug));
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

    /// <summary>
    /// Oracle Recruiting Cloud ("CandidateExperience"). The board a user lands on is a JS app whose
    /// URL carries the two things the anonymous REST endpoint needs: the tenant host and the site
    /// number. Accepts both the board URL and a job/requisition deep link, plus the REST URL itself
    /// (siteNumber then comes out of the `finder` param) — all four are things people paste.
    /// </summary>
    private static SourceCandidate? MatchOracleRecruiting(string host, Uri url, string[] segments)
    {
        if (!host.Contains(".oraclecloud.", StringComparison.Ordinal)) return null;

        var site = SiteNumberFromPath(segments) ?? SiteNumberFromQuery(url.Query);
        if (site is null) return null;

        var lang = LanguageFromPath(segments) ?? "en";
        // Unlike every other platform here, an Oracle URL names no company — the tenant label is an
        // opaque id ("ejqi", "fa-ewto-saasfaprod1"). Say what the source is and let the user (or link
        // discovery, which knows the careers page) supply the name; never pass the id off as one.
        var display = $"Oracle Recruiting Cloud ({site})";
        var draft = ApiDraft(
            name: $"oracle-{Slugify(host.Split('.')[0])}-{Slugify(site)}",
            display: display,
            endpoint: $"https://{host}/hcmRestApi/resources/latest/recruitingCEJobRequisitions",
            // `expand` is not optional: without it the requisition list comes back empty. `onlyData`
            // strips the HAL links that would otherwise dwarf the payload.
            query: new()
            {
                ["onlyData"] = "true",
                ["expand"] = "requisitionList.secondaryLocations",
                ["finder"] = $"findReqs;siteNumber={site},limit=200,sortBy=POSTING_DATES_DESC",
            },
            mapping: new()
            {
                // Requisitions sit one level down, inside the single search-result object.
                ["items_path"] = "items.0.requisitionList",
                ["id"] = "Id", ["title"] = "Title", ["location"] = "PrimaryLocation",
                ["url_template"] = $"https://{host}/hcmUI/CandidateExperience/{lang}/sites/{site}/job/{{Id}}",
                ["posted_at"] = "PostedDate",
            },
            // The list response carries no description — only the job page has one.
            enrichBody: true);
        return new SourceCandidate("oracle", display,
            $"Oracle Recruiting Cloud careers site ({site}) — fetched automatically.", draft);
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
            // Many job feeds (Jobindex/it-jobbank family) cap a page at ~20 items and honor a
            // `page` cursor. Size is left unset because a generic feed's page length is unknown;
            // the loop stops on the first empty page, or on the first page that adds nothing new
            // when a feed ignores `page` — so this is safe even for single-page feeds.
            Pagination: new PaginationConfig(Param: "page", Start: 1, Step: 1, MaxPages: 8),
            EnrichBody: true);
        return new SourceCandidate("rss", draft.DisplayName!,
            "Looks like a job feed — fetched automatically.", draft);
    }

    private static string? SiteNumberFromPath(string[] segments)
    {
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("sites", StringComparison.OrdinalIgnoreCase))
                return segments[i + 1];
        }
        return null;
    }

    private static string? SiteNumberFromQuery(string query)
    {
        const string marker = "siteNumber=";
        var idx = query.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var rest = query[(idx + marker.Length)..];
        var end = rest.IndexOfAny([',', '&', ';']);
        var value = end < 0 ? rest : rest[..end];
        return string.IsNullOrWhiteSpace(value) ? null : Uri.UnescapeDataString(value);
    }

    // ".../hcmUI/CandidateExperience/<lang>/sites/CX_1001/..." — the locale drives which language the
    // job pages come back in, so a Danish user's link keeps its Danish job pages.
    private static string? LanguageFromPath(string[] segments)
    {
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!segments[i].Equals("CandidateExperience", StringComparison.OrdinalIgnoreCase)) continue;
            var next = segments[i + 1];
            if (next.Length is 2 or 5 && next.All(c => char.IsAsciiLetter(c) || c == '-')) return next;
        }
        return null;
    }

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
}
