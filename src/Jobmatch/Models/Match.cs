namespace Jobmatch.Models;

public sealed record Match(
    Listing Listing,
    double Score,
    IReadOnlyDictionary<string, double> Breakdown,
    MatchReasoning Reasoning);
