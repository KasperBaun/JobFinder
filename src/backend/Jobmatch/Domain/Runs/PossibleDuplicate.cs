namespace Jobmatch.Domain.Runs;

/// <summary>
/// A pair the probabilistic matcher (R-117) could not settle: probably related, not confidently
/// the same ad. Persisted for the duplicates audit view; both listings keep their data.
/// SamePortal marks employer re-posts the same-portal rule refuses to merge — real hesitation,
/// but less interesting than a cross-portal pair the matcher may have missed.
/// </summary>
public sealed record PossibleDuplicate(
    string KeptId, string CandidateId, double Probability, bool SamePortal = false);
