using Jobmatch.Configuration;
using Jobmatch.Gui.Server.Models;
using Jobmatch.Models;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Gui.Server.Handlers;

public static class SkillsetHandler
{
    private static readonly object FileLock = new();

    public static IResult Get(Jobmatch.UserContext ctx)
    {
        try
        {
            var s = SkillsetParser.Load(ctx.SkillsetPath);
            return Results.Ok(ToResponse(s));
        }
        catch (Exception ex)
        {
            GuiLog.Error($"GET /api/skillset — {ex.GetType().Name}: {ex.Message}");
            return Results.Problem(ex.Message);
        }
    }

    public static IResult Put(SkillsetUpdateRequest? req, Jobmatch.UserContext ctx)
    {
        if (req is null)
        {
            return Results.Ok(new SaveResponse(false, "request body is required"));
        }
        try
        {
            lock (FileLock)
            {
                var existing = SkillsetParser.Load(ctx.SkillsetPath);
                var merged = Merge(existing, req);
                AtomicWriteText(ctx.SkillsetPath, SkillsetParser.Serialize(merged));
            }
            GuiLog.Action($"saved skillset.md ({req.Disqualifiers?.Count ?? 0} disqualifiers, {req.PrimaryStack?.Count ?? 0} primary stack items)");
            return Results.Ok(new SaveResponse(true));
        }
        catch (Exception ex)
        {
            GuiLog.Error($"PUT /api/skillset — {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException is { } inner)
                GuiLog.Error($"    inner — {inner.GetType().Name}: {inner.Message}");
            return Results.Ok(new SaveResponse(false, ex.Message));
        }
    }

    private static Skillset Merge(Skillset existing, SkillsetUpdateRequest req)
    {
        var name = req.Name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ConfigException("name must not be empty");
        var location = req.Location?.Trim();
        if (string.IsNullOrEmpty(location)) throw new ConfigException("location must not be empty");

        var experienceYears = req.ExperienceYears ?? existing.ExperienceYears;
        if (experienceYears < 0) throw new ConfigException("experienceYears must be >= 0");

        var remotePref = ParseEnum<RemotePreference>(req.RemotePreference, "remotePreference");
        var seniority = ParseEnum<Seniority>(req.Seniority, "seniority");

        return new Skillset(
            Name: name,
            Location: location,
            ExperienceYears: experienceYears,
            TargetRoles: CleanList(req.TargetRoles ?? existing.TargetRoles),
            RemotePreference: remotePref,
            Seniority: seniority,
            PrimaryStack: CleanList(req.PrimaryStack ?? existing.PrimaryStack),
            SecondaryStack: CleanList(req.SecondaryStack ?? existing.SecondaryStack),
            Domains: CleanList(req.Domains ?? existing.Domains),
            Disqualifiers: CleanList(req.Disqualifiers ?? existing.Disqualifiers),
            Languages: CleanList(req.Languages ?? existing.Languages),
            EmploymentTypes: CleanList(req.EmploymentTypes ?? existing.EmploymentTypes))
        {
            Country = NullIfBlank(req.Country),
            Region = NullIfBlank(req.Region),
            Metro = req.Metro is null ? existing.Metro : CleanList(req.Metro),
        };
    }

    public static SkillsetResponse ToResponse(Skillset s) => new(
        Name: s.Name,
        Location: s.Location,
        ExperienceYears: s.ExperienceYears,
        TargetRoles: s.TargetRoles,
        RemotePreference: s.RemotePreference.ToString().ToLowerInvariant(),
        Seniority: s.Seniority.ToString().ToLowerInvariant(),
        PrimaryStack: s.PrimaryStack,
        SecondaryStack: s.SecondaryStack,
        Domains: s.Domains,
        Disqualifiers: s.Disqualifiers,
        Languages: s.Languages,
        EmploymentTypes: s.EmploymentTypes,
        Country: s.Country,
        Region: s.Region,
        Metro: s.Metro);

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
