using System.Globalization;

namespace Jobmatch.Geo;

/// <summary>
/// Offline place index backing the radius filter (R-105): Danish postal codes, cities,
/// regions, world cities ≥ 100k population and country centroids, loaded from the
/// bundled <c>geo/gazetteer.tsv</c> (GeoNames CC-BY 4.0 + DAWA). A lookup matches a whole
/// segment — or, where one segment lists several sites, a whole conjunction-separated part of
/// it — never an arbitrary substring, with Danish/plain ASCII folding so "Århus" finds
/// "Aarhus". Resolution logic lives in Gazetteer.Resolve.cs.
/// </summary>
public sealed partial class Gazetteer
{
    private static readonly Lazy<Gazetteer> Bundled = new(() =>
        Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "geo", "gazetteer.tsv"))));

    private readonly Dictionary<string, List<GazetteerEntry>> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GazetteerEntry> _byPostal = new(StringComparer.Ordinal);

    private Gazetteer(IEnumerable<GazetteerEntry> entries)
    {
        foreach (var entry in entries) Index(entry);
        foreach (var bucket in _byName.Values)
        {
            bucket.Sort((a, b) => a.Type != b.Type
                ? a.Type.CompareTo(b.Type)
                : b.Population.CompareTo(a.Population));
        }
    }

    public static Gazetteer LoadBundled() => Bundled.Value;

    public static Gazetteer FromEntries(IEnumerable<GazetteerEntry> entries) => new(entries);

    public static Gazetteer Parse(string tsv)
    {
        var entries = new List<GazetteerEntry>();
        foreach (var raw in tsv.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            entries.Add(ParseLine(line));
        }
        return new Gazetteer(entries);
    }

    private static GazetteerEntry ParseLine(string line)
    {
        var f = line.Split('\t');
        if (f.Length != 7)
            throw new ConfigException($"gazetteer: expected 7 tab-separated fields, got {f.Length}: '{line}'");
        if (!Enum.TryParse<GeoPlaceType>(f[5], ignoreCase: true, out var type))
            throw new ConfigException($"gazetteer: unknown place type '{f[5]}'");
        return new GazetteerEntry(
            Name: f[0],
            Aliases: f[1].Length == 0 ? [] : f[1].Split('|'),
            Latitude: double.Parse(f[2], CultureInfo.InvariantCulture),
            Longitude: double.Parse(f[3], CultureInfo.InvariantCulture),
            CountryCode: f[4],
            Type: type,
            Population: long.Parse(f[6], CultureInfo.InvariantCulture));
    }

    private void Index(GazetteerEntry entry)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        AddKeys(keys, entry.Name);
        foreach (var alias in entry.Aliases)
        {
            if (entry.Type == GeoPlaceType.Postal && alias.Length == 4 && alias.All(char.IsAsciiDigit))
            {
                _byPostal.TryAdd(alias, entry);
                continue;
            }
            AddKeys(keys, alias);
        }

        foreach (var key in keys)
        {
            if (!_byName.TryGetValue(key, out var bucket))
            {
                bucket = [];
                _byName[key] = bucket;
            }
            bucket.Add(entry);
        }
    }

    private static void AddKeys(HashSet<string> keys, string name)
    {
        var exact = NormalizeKey(name);
        if (exact.Length == 0) return;
        keys.Add(exact);
        keys.Add(FoldDanish(exact));
        keys.Add(FoldPlain(exact));
    }
}
