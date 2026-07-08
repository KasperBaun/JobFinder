using Jobmatch.Search;
using Jobmatch.Services;

namespace Jobmatch.Llm;

// Listings the user marked good/bad in previous runs become few-shot examples
// for the LLM judge — closing the feedback loop so a mistake ("AI Engineer -
// Student" at 0.81 for a non-student) stops repeating once it's marked with a
// reason. Run ids are timestamps, so ordinal-descending = newest first; the
// newest opinion on a (title, company) wins across runs.
//
// Application outcomes calibrate too (R-098): interview/offer count as liked —
// a role type that reached interview is validated fit — and outrank plain marks
// when the cap is hit. An explicit bad mark overrides a positive outcome, and
// applied/rejected/no-response carry no fit signal on their own (rejection is
// an employer-side outcome, not evidence against the role type).
public static class MarkedExamplesLoader
{
    private const int MaxExamples = 12;

    public static IReadOnlyList<ExampleListing> Load(
        string historyDir,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ListingMark>> allMarks)
    {
        if (allMarks.Count == 0 || !Directory.Exists(historyDir)) return [];

        var candidates = new List<(ExampleListing Example, int Priority)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var runId in allMarks.Keys.OrderByDescending(id => id, StringComparer.Ordinal))
        {
            var path = Path.Combine(historyDir, $"{runId}.json");
            if (!File.Exists(path)) continue;
            var detail = HistoryService.TryReadDetail(path);
            if (detail is null) continue;

            foreach (var (listingId, mark) in allMarks[runId])
            {
                var example = Resolve(detail, listingId, mark);
                if (example is null || !seen.Add($"{example.Title}|{example.Company}")) continue;
                candidates.Add((example, Priority(mark)));
            }
        }

        // Stable sort: outcome-backed examples survive the cap first, newest-first within a tier.
        return candidates
            .OrderByDescending(c => c.Priority)
            .Take(MaxExamples)
            .Select(c => c.Example)
            .ToList();
    }

    private static int Priority(ListingMark mark) => mark switch
    {
        { Mark: "bad" } => 0,
        { Status: ApplicationStatus.Offer } => 2,
        { Status: ApplicationStatus.Interview } => 1,
        _ => 0,
    };

    // Marks can target any scored listing (longlist table), not just the shortlist.
    private static ExampleListing? Resolve(RunDetail detail, string listingId, ListingMark mark)
    {
        var positiveOutcome = mark.Status is ApplicationStatus.Interview or ApplicationStatus.Offer;
        var polarity = mark.Mark == "good" ? "liked"
            : mark.Mark == "bad" ? "disliked"
            : positiveOutcome ? "liked"
            : null;
        if (polarity is null) return null;

        var note = polarity == "liked" && positiveOutcome
            ? MergeNote(mark.Reason, mark.Status == ApplicationStatus.Offer ? "received an offer" : "reached interview")
            : mark.Reason;

        var scored = detail.Scored?.FirstOrDefault(s => string.Equals(s.Id, listingId, StringComparison.Ordinal));
        if (scored is not null)
        {
            return new ExampleListing(polarity, scored.Title, scored.Company ?? string.Empty,
                scored.Location, Seniority: null, scored.PrimaryStackHits, Domains: [],
                EmployerType: null, Note: note);
        }

        var match = detail.Shortlist.FirstOrDefault(m => string.Equals(m.Id, listingId, StringComparison.Ordinal));
        if (match is not null)
        {
            return new ExampleListing(polarity, match.Title, match.Company ?? string.Empty,
                match.Location, Seniority: null, match.PrimaryStackHits, Domains: [],
                EmployerType: null, Note: note);
        }

        return null;
    }

    private static string MergeNote(string? reason, string statusNote)
        => reason is null ? statusNote : $"{reason} — {statusNote}";
}
