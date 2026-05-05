namespace Jobmatch.Gui.Server.Models;

public sealed record SkillsetResponse(
    string Name,
    string Location,
    int ExperienceYears,
    IReadOnlyList<string> TargetRoles,
    string RemotePreference,
    string Seniority,
    IReadOnlyList<string> PrimaryStack,
    IReadOnlyList<string> SecondaryStack,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Disqualifiers,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> EmploymentTypes);
