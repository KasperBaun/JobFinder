namespace Jobmatch.Search;

/// <summary>Per-provider fetch outcome within a run. Serialised as a camelCase string ("pending"/"running"/"ok"/"failed").</summary>
public enum ProviderRunState
{
    Pending,
    Running,
    Ok,
    Failed,
}

/// <summary>
/// Per-provider outcome for a single search run, persisted in history files.
/// </summary>
public sealed record ProviderRunStatus(
    string Name,
    ProviderRunState Status,
    int? FetchedCount,
    string? Error,
    long? DurationMs = null,
    bool HitPageCap = false,
    bool PossiblyCapped = false);
