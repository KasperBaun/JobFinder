namespace Jobmatch.Api.Models;

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
    IReadOnlyList<string> EmploymentTypes,
    string? Country,
    string? Region,
    IReadOnlyList<string> Metro);

public sealed record SkillsetUpdateRequest(
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
    IReadOnlyList<string>? Metro);

public sealed record SaveResponse(bool Success, string? Error = null);
