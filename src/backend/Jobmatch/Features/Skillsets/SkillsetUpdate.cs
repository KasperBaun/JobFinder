namespace Jobmatch.Features.Skillsets;

/// <summary>
/// A requested profile change. Deliberately carries no coordinates: those are derived from
/// <see cref="SkillsetUpdate.Address"/> by the service at save time (R-105), so there is no way for
/// a caller to save a profile whose stored position disagrees with its address.
/// </summary>

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
    double? RadiusKm = null);
