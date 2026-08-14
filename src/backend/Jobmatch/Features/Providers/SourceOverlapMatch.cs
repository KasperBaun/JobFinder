namespace Jobmatch.Features.Providers;

/// <summary>
/// How much a freshly fetched source overlaps a source the user already has. <see cref="Ratio"/> is
/// measured against the smaller of the two sets, so a board that is fully contained in a bigger
/// aggregator still reads as 1.0 rather than being diluted by the aggregator's other jobs.
/// </summary>
public sealed record SourceOverlapMatch(
    int ProviderId,
    string DisplayName,
    int ExistingCount,
    int SharedCount,
    double Ratio,
    bool Duplicate);
