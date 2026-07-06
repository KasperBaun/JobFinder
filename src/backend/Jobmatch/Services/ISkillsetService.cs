using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Services;

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
    IReadOnlyList<string>? PreferredCompanies = null);

public interface ISkillsetService
{
    Skillset Get();
    Skillset Update(SkillsetUpdate input);
}

public sealed class SkillsetService(UserContext ctx) : ISkillsetService
{
    private readonly object _fileLock = new();

    public Skillset Get()
    {
        if (!File.Exists(ctx.SkillsetPath))
            throw new InvalidRequestException("No profile set up yet.");
        return SkillsetParser.Load(ctx.SkillsetPath);
    }

    public Skillset Update(SkillsetUpdate input)
    {
        lock (_fileLock)
        {
            // Create-or-update: on first write there is no file yet, so merge onto an empty baseline.
            var existing = File.Exists(ctx.SkillsetPath)
                ? SkillsetParser.Load(ctx.SkillsetPath)
                : EmptyBaseline();
            var merged = Merge(existing, input);
            AtomicWriteText(ctx.SkillsetPath, SkillsetParser.Serialize(merged));
            return merged;
        }
    }

    private static Skillset EmptyBaseline() => new(
        Name: "",
        Location: "",
        ExperienceYears: 0,
        TargetRoles: [],
        RemotePreference: RemotePreference.Any,
        Seniority: Seniority.Any,
        PrimaryStack: [],
        SecondaryStack: [],
        Domains: [],
        Disqualifiers: [],
        Languages: [],
        EmploymentTypes: []);

    private static Skillset Merge(Skillset existing, SkillsetUpdate input)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ConfigException("name must not be empty");
        var location = input.Location?.Trim();
        if (string.IsNullOrEmpty(location)) throw new ConfigException("location must not be empty");

        var experienceYears = input.ExperienceYears ?? existing.ExperienceYears;
        if (experienceYears < 0) throw new ConfigException("experienceYears must be >= 0");

        var remotePref = ParseEnum<RemotePreference>(input.RemotePreference, "remotePreference");
        var seniority = ParseEnum<Seniority>(input.Seniority, "seniority");

        return new Skillset(
            Name: name,
            Location: location,
            ExperienceYears: experienceYears,
            TargetRoles: CleanList(input.TargetRoles ?? existing.TargetRoles),
            RemotePreference: remotePref,
            Seniority: seniority,
            PrimaryStack: CleanList(input.PrimaryStack ?? existing.PrimaryStack),
            SecondaryStack: CleanList(input.SecondaryStack ?? existing.SecondaryStack),
            Domains: CleanList(input.Domains ?? existing.Domains),
            Disqualifiers: CleanList(input.Disqualifiers ?? existing.Disqualifiers),
            Languages: CleanList(input.Languages ?? existing.Languages),
            EmploymentTypes: CleanList(input.EmploymentTypes ?? existing.EmploymentTypes))
        {
            Country = NullIfBlank(input.Country),
            Region = NullIfBlank(input.Region),
            Metro = input.Metro is null ? existing.Metro : CleanList(input.Metro),
            PreferredCompanies = input.PreferredCompanies is null ? existing.PreferredCompanies : CleanList(input.PreferredCompanies),
        };
    }

    private static T ParseEnum<T>(string? raw, string fieldName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ConfigException($"{fieldName} is required");
        if (!Enum.TryParse<T>(raw, ignoreCase: true, out var v))
            throw new ConfigException($"{fieldName} must be one of [{string.Join(", ", Enum.GetNames<T>()).ToLowerInvariant()}], got '{raw}'");
        return v;
    }

    private static IReadOnlyList<string> CleanList(IEnumerable<string> source) =>
        source.Select(x => x?.Trim() ?? string.Empty).Where(s => s.Length > 0).ToList();

    private static string? NullIfBlank(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void AtomicWriteText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}
