namespace Jobmatch.Services;

public sealed partial class MarksService(UserContext ctx) : IMarksService
{
    private const int MaxReasonLength = 500;

    private readonly object _fileLock = new();

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, ListingMark>> LoadAll()
    {
        var raw = LoadMutable();
        return raw.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyDictionary<string, ListingMark>)kvp.Value);
    }

    public IReadOnlyDictionary<string, ListingMark> GetForRun(string runId)
    {
        var all = LoadMutable();
        if (all.TryGetValue(runId, out var byListing))
        {
            return new Dictionary<string, ListingMark>(byListing, StringComparer.Ordinal);
        }
        return new Dictionary<string, ListingMark>(StringComparer.Ordinal);
    }

    public void Set(string runId, string listingId, string? mark, string? reason)
    {
        ValidateIds(runId, listingId);

        var normalised = mark?.ToLowerInvariant();
        if (normalised is not (null or "good" or "bad"))
            throw new InvalidRequestException("mark must be 'good', 'bad', or null");

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason is { Length: > MaxReasonLength })
            throw new InvalidRequestException($"reason must be {MaxReasonLength} characters or fewer");

        // Clearing the mark drops the reason too (it explained the old mark, R-094)
        // but never the status — the entry survives while either half is set (R-096).
        Update(runId, listingId, existing =>
            existing with { Mark = normalised, Reason = normalised is null ? null : trimmedReason });
    }

    public void SetStatus(string runId, string listingId, string? status)
    {
        ValidateIds(runId, listingId);

        var normalised = status?.ToLowerInvariant();
        if (normalised is not null && !ApplicationStatus.IsValid(normalised))
            throw new InvalidRequestException(
                $"status must be one of {string.Join(", ", ApplicationStatus.All)}, or null");

        Update(runId, listingId, existing => existing with { Status = normalised });
    }

    public void RemoveRuns(IEnumerable<string> runIds)
    {
        lock (_fileLock)
        {
            var all = LoadMutable();
            var changed = false;
            foreach (var id in runIds)
            {
                if (all.Remove(id)) changed = true;
            }
            if (changed) AtomicWrite(all);
        }
    }

    private static void ValidateIds(string runId, string listingId)
    {
        if (string.IsNullOrWhiteSpace(runId)) throw new InvalidRequestException("runId is required");
        if (string.IsNullOrWhiteSpace(listingId)) throw new InvalidRequestException("listingId is required");
    }

    private void Update(string runId, string listingId, Func<ListingMark, ListingMark> apply)
    {
        lock (_fileLock)
        {
            var all = LoadMutable();

            var existing = all.TryGetValue(runId, out var current) && current.TryGetValue(listingId, out var entry)
                ? entry
                : new ListingMark(null, null);
            var updated = apply(existing);

            if (updated is { Mark: null, Status: null })
            {
                if (all.TryGetValue(runId, out var byListing))
                {
                    byListing.Remove(listingId);
                    if (byListing.Count == 0) all.Remove(runId);
                }
            }
            else
            {
                if (!all.TryGetValue(runId, out var byListing))
                {
                    byListing = new Dictionary<string, ListingMark>(StringComparer.Ordinal);
                    all[runId] = byListing;
                }
                byListing[listingId] = updated;
            }

            AtomicWrite(all);
        }
    }
}
