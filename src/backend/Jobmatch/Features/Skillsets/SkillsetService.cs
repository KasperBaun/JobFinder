using Jobmatch.Domain;
using Jobmatch.Platform.IO;
using Jobmatch.Platform.Paths;

namespace Jobmatch.Features.Skillsets;

public sealed class SkillsetService(UserContext ctx, IGeocodingService geocoding) : ISkillsetService
{
    private readonly object _fileLock = new();

    public Skillset Get()
    {
        if (!File.Exists(ctx.SkillsetPath))
            throw new InvalidRequestException("No profile set up yet.");
        return SkillsetParser.Load(ctx.SkillsetPath);
    }

    public Skillset? Find() => File.Exists(ctx.SkillsetPath) ? SkillsetParser.Load(ctx.SkillsetPath) : null;

    public async Task<Skillset> UpdateAsync(SkillsetUpdate input, CancellationToken ct = default)
    {
        // Resolving coordinates is a network call, so it happens before the lock rather than inside it.
        var geocode = await ResolveCoordinatesAsync(input.Address, ct).ConfigureAwait(false);

        lock (_fileLock)
        {
            // Create-or-update: on first write there is no file yet, so merge onto an empty baseline.
            var existing = File.Exists(ctx.SkillsetPath)
                ? SkillsetParser.Load(ctx.SkillsetPath)
                : EmptyBaseline();
            var merged = Merge(existing, input, geocode);
            AtomicFile.WriteAllText(ctx.SkillsetPath, SkillsetParser.Serialize(merged));
            return merged;
        }
    }

    // Geocode only when the address is new or was never resolved; an unchanged address keeps its
    // stored coordinates without a network call, and a blank address clears everything. A failed
    // geocode still saves — the coordinates just stay empty (R-105).
    private async Task<GeocodeResult?> ResolveCoordinatesAsync(string? rawAddress, CancellationToken ct)
    {
        var address = rawAddress?.Trim();
        if (string.IsNullOrEmpty(address)) return null;

        var existing = Find();
        if (existing is { Latitude: double lat, Longitude: double lon }
            && string.Equals(existing.Address, address, StringComparison.Ordinal))
        {
            return new GeocodeResult(lat, lon, existing.ResolvedAddress ?? address);
        }

        return await geocoding.GeocodeAsync(address, ct).ConfigureAwait(false);
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

    private static Skillset Merge(Skillset existing, SkillsetUpdate input, GeocodeResult? geocode)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ConfigException("name must not be empty");
        var location = input.Location?.Trim();
        if (string.IsNullOrEmpty(location)) throw new ConfigException("location must not be empty");

        var experienceYears = input.ExperienceYears ?? existing.ExperienceYears;
        if (experienceYears < 0) throw new ConfigException("experienceYears must be >= 0");

        var radiusKm = input.RadiusKm ?? existing.RadiusKm;
        if (radiusKm is < 0) throw new ConfigException("radiusKm must be >= 0");

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
            Address = NullIfBlank(input.Address),
            RadiusKm = radiusKm,
            Latitude = geocode?.Latitude,
            Longitude = geocode?.Longitude,
            ResolvedAddress = geocode?.ResolvedAddress,
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
}
