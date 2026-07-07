namespace Jobmatch.Models;

// What the LLM could read out of a CV (R-011). Every field is optional — only
// facts the CV states are reported, and the GUI uses the result to prefill the
// profile form; nothing persists until the user reviews and saves (R-012).
// Seniority and RemotePreference stay strings here: they are validated against
// the enum names at parse time and the form consumes the lowercase values as-is.
// Disqualifiers and preferred companies are deliberately absent — a CV can't
// state them.
public sealed record ExtractedProfile(
    string? Name,
    string? Location,
    string? Country,
    string? Region,
    IReadOnlyList<string> Metro,
    int? ExperienceYears,
    string? Seniority,
    string? RemotePreference,
    IReadOnlyList<string> TargetRoles,
    IReadOnlyList<string> PrimaryStack,
    IReadOnlyList<string> SecondaryStack,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> EmploymentTypes);
