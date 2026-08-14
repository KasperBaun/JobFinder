using System.Globalization;
using System.Text.RegularExpressions;
using Jobmatch.Models;

namespace Jobmatch.Services;

/// <summary>
/// Recognises the common cases a non-technical user can add by pasting a URL — job boards on the
/// major ATS platforms (Greenhouse, Ashby, Lever, SmartRecruiters, Teamtailor, HR-Manager, Oracle
/// Recruiting Cloud) plus generic RSS feeds. The generated config mirrors a proven catalog entry for
/// each platform, so the mapping is known-good rather than authored on the fly. Anything
/// unrecognised yields no candidate; the caller then tries link discovery, and finally manual import.
/// The matchers themselves live in SourceDetectionService.Boards.cs.
/// </summary>
public sealed partial class SourceDetectionService : ISourceDetectionService
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
            MatchOracleRecruiting(host, url, segments) ??
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

    /// <summary>
    /// Re-labels a candidate with a brand name learned outside the URL — link discovery knows the
    /// careers page it crawled, while the ATS URL itself often carries only an opaque tenant id
    /// (Oracle's "ejqi", "fa-ewto-saasfaprod1"). The stored name follows so the source does not sit
    /// in the list as "oracle-ejqi-cx-1001".
    /// </summary>
    internal static SourceCandidate WithBrand(SourceCandidate candidate, string? brand)
    {
        if (string.IsNullOrWhiteSpace(brand)) return candidate;

        var pretty = brand.Trim();
        var slug = Slugify(pretty);
        var platform = candidate.Draft.Name.Split('-')[0];
        var fields = candidate.Draft.StaticFields is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(candidate.Draft.StaticFields);
        fields["company"] = pretty;

        var draft = candidate.Draft with
        {
            Name = $"{platform}-{slug}",
            DisplayName = pretty,
            StaticFields = fields,
        };
        return candidate with { DisplayName = pretty, Draft = draft };
    }

    /// <param name="company">
    /// Stamped on every listing the board returns. Pass null when the URL carries no real company
    /// name — an opaque tenant id is worse than no company at all, because it would then travel with
    /// all 140 jobs as if it were the employer.
    /// </param>
    private static PortalConfig ApiDraft(
        string name,
        string display,
        string endpoint,
        Dictionary<string, object?>? query,
        Dictionary<string, string> mapping,
        PaginationConfig? pagination = null,
        bool enrichBody = false,
        string? company = null) =>
        new(
            Name: name,
            Type: PortalType.Api,
            Enabled: true,
            Endpoint: new Uri(endpoint),
            QueryParams: query,
            ResponseMapping: mapping,
            StaticFields: company is null ? null : new Dictionary<string, string> { ["company"] = company },
            DisplayName: display,
            Pagination: pagination,
            EnrichBody: enrichBody);

    /// <summary>Title-cases a raw host label ("danskebank") for use as a display name.</summary>
    internal static string PrettifyBrand(string label) => Prettify(label);

    private static string Prettify(string slug)
    {
        var s = Regex.Replace(slug.Replace('-', ' ').Replace('_', ' '), @"\d+$", "").Trim();
        if (s.Length == 0) s = slug;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);
    }

    private static string Slugify(string s) =>
        Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
}
