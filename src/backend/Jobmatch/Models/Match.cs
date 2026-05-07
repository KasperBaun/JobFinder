namespace Jobmatch.Models;

public sealed record Match(
    Listing Listing,
    double Score,
    ScoreBreakdown Breakdown,
    MatchReasoning Reasoning);
