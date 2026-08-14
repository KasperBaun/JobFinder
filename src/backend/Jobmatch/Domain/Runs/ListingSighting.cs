namespace Jobmatch.Domain.Runs;

/// <summary>
/// Another portal's copy of a shortlisted ad, folded into the same slot by the probabilistic
/// matcher (R-117) instead of spending a slot of its own.
/// </summary>
public sealed record ListingSighting(
    string Id,
    string Portal,
    string? PortalDisplayName,
    string Title,
    string Url,
    double Probability);
