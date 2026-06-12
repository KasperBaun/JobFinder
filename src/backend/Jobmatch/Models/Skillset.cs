namespace Jobmatch.Models;

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
}
