namespace Jobmatch.Domain;

public sealed record Match(
    Listing Listing,
    double Score,
    ScoreBreakdown Breakdown,
    MatchReasoning Reasoning);
