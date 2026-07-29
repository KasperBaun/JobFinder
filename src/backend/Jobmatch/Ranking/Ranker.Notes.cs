using Jobmatch.Models;

namespace Jobmatch.Ranking;

/// <summary>
/// Builds a match's rationale as structured <see cref="ReasoningNote"/>s, plus the English rendering
/// of the same list. Split from Ranker.cs to keep both files inside the 300-line limit.
/// </summary>
public static partial class Ranker
{
    // Keys are persisted in run history — additive only. Mirrored by the frontend's
    // src/frontend/src/i18n/serverKeys.ts.
    internal static IReadOnlyList<ReasoningNote> BuildNotes(
        IReadOnlyList<string> primaryHits,
        IReadOnlyList<string> secondaryHits,
        IReadOnlyList<string> domainHits,
        bool? seniorityMatch,
        bool seniorityIsAdjacent,
        bool? locationMatch,
        bool? remoteMatch,
        IReadOnlyList<string> disqualifierHits,
        bool nonEngineeringTitle,
        Listing listing,
        double? ageDays,
        double halfLifeDays)
    {
        if (disqualifierHits.Count > 0)
        {
            return [new ReasoningNote("disqualified", Args(("hits", disqualifierHits)))];
        }

        // The preferred-company boost is deliberately absent here — it renders as a
        // badge in the GUI (from Breakdown.PreferredCompanyBonus), not as note prose.
        var notes = new List<ReasoningNote>
        {
            primaryHits.Count > 0
                ? new ReasoningNote("primaryHits", Args(("skills", primaryHits)))
                : new ReasoningNote("noPrimaryHits"),
        };

        if (secondaryHits.Count > 0)
        {
            notes.Add(new ReasoningNote("secondaryHits", Args(("skills", secondaryHits))));
        }
        if (domainHits.Count > 0)
        {
            notes.Add(new ReasoningNote("domainHits", Args(("domains", domainHits))));
        }

        notes.Add(new ReasoningNote((seniorityMatch, seniorityIsAdjacent) switch
        {
            (true, true) => "seniorityClose",
            (true, false) => "seniorityMatches",
            (false, _) => "seniorityMismatch",
            (null, _) => "seniorityUnknown",
        }));

        if (nonEngineeringTitle)
        {
            notes.Add(new ReasoningNote("titleNotDeveloper"));
        }

        notes.Add((locationMatch, remoteMatch) switch
        {
            (true, _) => new ReasoningNote("location", Args(("location", listing.Location ?? string.Empty))),
            (_, true) => new ReasoningNote("remoteOk", Args(("mode", listing.RemoteMode.ToString().ToLowerInvariant()))),
            (false, false) => new ReasoningNote("neitherLocationNorRemote"),
            (null, null) => new ReasoningNote("locationRemoteUnknown"),
            (false, null) => new ReasoningNote("locationMismatchRemoteUnknown"),
            (null, false) => new ReasoningNote("locationUnknownRemoteMismatch"),
        });

        if (ageDays is double age && age > 2 * halfLifeDays)
        {
            notes.Add(new ReasoningNote("agePenalty", Args(("days", (int)Math.Round(age)))));
        }

        return notes;
    }

    /// <summary>
    /// The English rendering, kept so <see cref="MatchReasoning.Notes"/> stays populated for
    /// top-jobs.md, logs, and any client reading a run recorded before the keys existed.
    /// </summary>
    internal static string RenderEnglish(IReadOnlyList<ReasoningNote> notes) =>
        string.Join(" ", notes.Select(RenderOne));

    private static string RenderOne(ReasoningNote note) => note.Key switch
    {
        "disqualified" => $"Removed because of: {Join(note, "hits")}.",
        "primaryHits" => $"Must-have skill match: {Join(note, "skills")}.",
        "noPrimaryHits" => "None of your must-have skills mentioned.",
        "secondaryHits" => $"Also matches: {Join(note, "skills")}.",
        "domainHits" => $"Industry: {Join(note, "domains")}.",
        "seniorityClose" => "Experience level close fit.",
        "seniorityMatches" => "Experience level matches.",
        "seniorityMismatch" => "Experience level doesn't match.",
        "seniorityUnknown" => "Experience level not stated.",
        "titleNotDeveloper" => "Title doesn't look like a developer role — rating reduced.",
        "location" => $"Location: {Text(note, "location")}.",
        "remoteOk" => $"Remote work OK ({Text(note, "mode")}).",
        "neitherLocationNorRemote" => "Neither location nor remote setup matches.",
        "locationRemoteUnknown" => "Location and remote setup not stated.",
        "locationMismatchRemoteUnknown" => "Location doesn't match; remote setup not stated.",
        "locationUnknownRemoteMismatch" => "Location not stated; remote setup doesn't match.",
        "agePenalty" => $"Posted {Text(note, "days")} days ago — rating reduced for age.",
        _ => string.Empty,
    };

    private static IReadOnlyDictionary<string, object> Args(params (string Key, object Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    private static string Join(ReasoningNote note, string key) =>
        note.Args?.TryGetValue(key, out var value) == true && value is IEnumerable<string> items
            ? string.Join(", ", items)
            : string.Empty;

    private static string Text(ReasoningNote note, string key) =>
        note.Args?.TryGetValue(key, out var value) == true ? value?.ToString() ?? string.Empty : string.Empty;
}
