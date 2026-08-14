namespace Jobmatch.Features.Skillsets;

public sealed record SkillsetUpdate(
    string? Name,
    string? Location,
    int? ExperienceYears,
    IReadOnlyList<string>? TargetRoles,
    string? RemotePreference,
    string? Seniority,
    IReadOnlyList<string>? PrimaryStack,
    IReadOnlyList<string>? SecondaryStack,
    IReadOnlyList<string>? Domains,
    IReadOnlyList<string>? Disqualifiers,
    IReadOnlyList<string>? Languages,
    IReadOnlyList<string>? EmploymentTypes,
    string? Country,
    string? Region,
    IReadOnlyList<string>? Metro,
    IReadOnlyList<string>? PreferredCompanies = null,
    string? Address = null,
    double? RadiusKm = null,
    // Server-computed by the geocoding step in SkillsetHandler — never client input.
    double? Latitude = null,
    double? Longitude = null,
    string? ResolvedAddress = null);
