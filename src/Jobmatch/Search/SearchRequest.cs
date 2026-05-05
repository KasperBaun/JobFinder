namespace Jobmatch.Search;

/// <summary>
/// Optional parameters for a search run. Null/empty Providers means "all enabled portals".
/// TopN/MinScore null fall back to the values in ranking config.
/// </summary>
public sealed record SearchRequest(
    IReadOnlyList<string>? Providers = null,
    int? TopN = null,
    double? MinScore = null);
