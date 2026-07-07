using Jobmatch.Search;

namespace Jobmatch.Services;

// Aggregates every statused listing across runs. Listing ids are stable SHA-256
// hashes (BaseAdapter.StableId), so the same posting statused in several runs
// dedupes to one entry — run ids are timestamps, so ordinal-descending iteration
// makes the newest run's status win (same convention as MarkedExamplesLoader).
public sealed class ApplicationsService(UserContext ctx, IMarksService marks) : IApplicationsService
{
    private static readonly IReadOnlyDictionary<string, int> ActivityOrder = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [ApplicationStatus.Offer] = 0,
        [ApplicationStatus.Interview] = 1,
        [ApplicationStatus.Applied] = 2,
        [ApplicationStatus.NoResponse] = 3,
        [ApplicationStatus.Rejected] = 4,
    };

    public IReadOnlyList<ApplicationEntry> List()
    {
        var allMarks = marks.LoadAll();
        if (allMarks.Count == 0 || !Directory.Exists(ctx.HistoryDir)) return [];

        var entries = new List<ApplicationEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var runId in allMarks.Keys.OrderByDescending(id => id, StringComparer.Ordinal))
        {
            RunDetail? detail = null;
            foreach (var (listingId, mark) in allMarks[runId])
            {
                if (mark.Status is null || seen.Contains(listingId)) continue;

                detail ??= LoadDetail(runId);
                if (detail is null) break;

                var entry = Resolve(detail, listingId, mark);
                if (entry is null) continue;
                seen.Add(listingId);
                entries.Add(entry);
            }
        }

        return entries
            .OrderBy(e => ActivityOrder.GetValueOrDefault(e.Status, int.MaxValue))
            .ThenByDescending(e => e.RunId, StringComparer.Ordinal)
            .ToList();
    }

    private RunDetail? LoadDetail(string runId)
    {
        var path = Path.Combine(ctx.HistoryDir, $"{runId}.json");
        return File.Exists(path) ? HistoryService.TryReadDetail(path) : null;
    }

    // Statuses can target any scored listing (longlist table), not just the shortlist.
    private static ApplicationEntry? Resolve(RunDetail detail, string listingId, ListingMark mark)
    {
        var scored = detail.Scored?.FirstOrDefault(s => string.Equals(s.Id, listingId, StringComparison.Ordinal));
        if (scored is not null)
        {
            return new ApplicationEntry(listingId, detail.RunId, detail.StartedAt, mark.Status!,
                mark.Mark, mark.Reason, scored.Title, scored.Company, scored.Location,
                scored.Url, scored.Portal, scored.PortalDisplayName, scored.Score);
        }

        var match = detail.Shortlist.FirstOrDefault(m => string.Equals(m.Id, listingId, StringComparison.Ordinal));
        if (match is not null)
        {
            return new ApplicationEntry(listingId, detail.RunId, detail.StartedAt, mark.Status!,
                mark.Mark, mark.Reason, match.Title, match.Company, match.Location,
                match.Url, match.Portal, match.PortalDisplayName, match.Score);
        }

        return null;
    }
}
