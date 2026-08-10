namespace Jobmatch.Deduplication;

/// <summary>
/// A shortlist-time pair the probabilistic matcher (R-116) could not settle: probably related,
/// not confidently the same ad. Persisted for the duplicates audit view; both listings keep
/// their own slot and data.
/// </summary>
public sealed record PossibleDuplicate(string KeptId, string CandidateId, double Probability);
