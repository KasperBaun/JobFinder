namespace Jobmatch.Services;

public interface IMarksService
{
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ListingMark>> LoadAll();
    IReadOnlyDictionary<string, ListingMark> GetForRun(string runId);
    void Set(string runId, string listingId, string? mark, string? reason);
    void SetStatus(string runId, string listingId, string? status);
    void RemoveRuns(IEnumerable<string> runIds);
}
