using Jobmatch.Models;
using Match = Jobmatch.Models.Match;

namespace Jobmatch.Search;

public sealed partial class SearchService
{
    private static ListingMatch ToListingMatch(
        Match match,
        IReadOnlyDictionary<string, string> portalDisplayNames,
        IReadOnlyList<ListingSighting>? sightings = null)
    {
        var l = match.Listing;
        return new ListingMatch(
            Id: l.Id,
            Portal: l.Portal,
            Title: l.Title,
            Company: l.Company,
            Location: l.Location,
            RemoteMode: l.RemoteMode.ToString().ToLowerInvariant(),
            Url: l.Url.ToString(),
            PostedAt: l.PostedAt,
            Score: match.Score,
            Reasoning: match.Reasoning.Notes,
            PrimaryStackHits: match.Reasoning.PrimaryStackHits,
            SecondaryStackHits: match.Reasoning.SecondaryStackHits,
            PortalDisplayName: portalDisplayNames.TryGetValue(l.Portal, out var dn) ? dn : l.Portal,
            FavoriteCompany: match.Breakdown.PreferredCompanyBonus > 0,
            ReasoningNotes: match.Reasoning.NoteKeys,
            Description: string.IsNullOrWhiteSpace(l.Description) ? null : l.Description,
            LlmScore: match.Reasoning.LlmScore,
            LlmReason: match.Reasoning.LlmReason,
            Sightings: sightings);
    }

    private static IReadOnlyList<ListingSighting>? ToSightings(
        Match primary, ShortlistSelection selection, IReadOnlyDictionary<string, string> portalDisplayNames)
    {
        if (!selection.SightingsByPrimary.TryGetValue(primary.Listing.Id, out var absorbed)) return null;
        return absorbed.Select(s => new ListingSighting(
            Id: s.Match.Listing.Id,
            Portal: s.Match.Listing.Portal,
            PortalDisplayName: portalDisplayNames.TryGetValue(s.Match.Listing.Portal, out var dn) ? dn : s.Match.Listing.Portal,
            Title: s.Match.Listing.Title,
            Url: s.Match.Listing.Url.ToString(),
            Probability: Math.Round(s.Probability, 2))).ToList();
    }

    private static RawListing ToRawListing(Listing l) => new(
        Id: l.Id,
        Title: l.Title,
        Company: l.Company,
        Location: l.Location,
        Url: l.Url.ToString(),
        PostedAt: l.PostedAt);

    private static ScoredEntry ToScoredEntry(Match m, IReadOnlyDictionary<string, string> portalDisplayNames) => new(
        Id: m.Listing.Id,
        Title: m.Listing.Title,
        Company: m.Listing.Company,
        Location: m.Listing.Location,
        Url: m.Listing.Url.ToString(),
        PostedAt: m.Listing.PostedAt,
        Portal: m.Listing.Portal,
        Score: m.Score,
        Breakdown: m.Breakdown,
        PrimaryStackHits: m.Reasoning.PrimaryStackHits,
        SecondaryStackHits: m.Reasoning.SecondaryStackHits,
        PortalDisplayName: portalDisplayNames.TryGetValue(m.Listing.Portal, out var dn) ? dn : m.Listing.Portal);
}
