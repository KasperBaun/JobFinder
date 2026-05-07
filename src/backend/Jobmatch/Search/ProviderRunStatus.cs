namespace Jobmatch.Search;

/// <summary>
/// Per-provider outcome for a single search run, persisted in history files.
/// </summary>
public sealed record ProviderRunStatus(
    string Name,
    string Status,
    int? FetchedCount,
    string? Error);
