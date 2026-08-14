namespace Jobmatch.Domain;

public enum RemotePreference { Onsite, Hybrid, Remote, Any }

public enum Seniority { Junior, Mid, Senior, Lead, Any }

public sealed record Skillset(
    string Name,
    string Location,
    int ExperienceYears,
    IReadOnlyList<string> TargetRoles,
    RemotePreference RemotePreference,
    Seniority Seniority,
    IReadOnlyList<string> PrimaryStack,
    IReadOnlyList<string> SecondaryStack,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Disqualifiers,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> EmploymentTypes)
{
    public string? Country { get; init; }
    public string? Region { get; init; }
    public IReadOnlyList<string> Metro { get; init; } = [];
    public IReadOnlyList<string> PreferredCompanies { get; init; } = [];

    // Radius filter (R-105). Coordinates are server-computed at save time (DAWA);
    // the filter is active only when both coordinates and a positive RadiusKm exist.
    public string? Address { get; init; }
    public double? RadiusKm { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? ResolvedAddress { get; init; }
}
