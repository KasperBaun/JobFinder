using System.Text;
using System.Text.RegularExpressions;
using Jobmatch.Models;
using YamlDotNet.Serialization;

namespace Jobmatch.Configuration;

public static class SkillsetParser
{
    private const string PrimarySection = "Primary stack";
    private const string SecondarySection = "Secondary stack";
    private const string DomainsSection = "Domains";
    private const string DisqualifiersSection = "Disqualifiers";

    private static readonly Regex FrontmatterRegex = new(
        @"\A---\s*\r?\n(?<fm>.*?)\r?\n---\s*\r?\n(?<body>.*)\Z",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();
    private static readonly ISerializer Serializer = new SerializerBuilder().Build();

    public static Skillset Parse(string content)
    {
        var match = FrontmatterRegex.Match(content);
        if (!match.Success)
        {
            throw new ConfigException("skillset: expected YAML frontmatter delimited by '---' lines at start of file");
        }

        IReadOnlyDictionary<string, object?> yaml;
        try
        {
            yaml = DeserializeYaml(match.Groups["fm"].Value);
        }
        catch (Exception ex) when (ex is not ConfigException)
        {
            throw new ConfigException($"skillset frontmatter: YAML parse error — {ex.Message}", ex);
        }

        var sections = ParseSections(match.Groups["body"].Value);
        return Build(yaml, sections);
    }

    public static Skillset Load(string path) => Parse(File.ReadAllText(path));

    public static string Serialize(Skillset skillset)
    {
        var frontmatter = new Dictionary<string, object?>
        {
            ["name"] = skillset.Name,
            ["location"] = skillset.Location,
            ["experience_years"] = skillset.ExperienceYears,
            ["target_roles"] = skillset.TargetRoles.ToList(),
            ["remote_preference"] = skillset.RemotePreference.ToString().ToLowerInvariant(),
            ["seniority"] = skillset.Seniority.ToString().ToLowerInvariant(),
            ["languages"] = skillset.Languages.ToList(),
            ["employment_types"] = skillset.EmploymentTypes.ToList(),
        };
        if (!string.IsNullOrWhiteSpace(skillset.Country)) frontmatter["country"] = skillset.Country;
        if (!string.IsNullOrWhiteSpace(skillset.Region)) frontmatter["region"] = skillset.Region;
        if (skillset.Metro.Count > 0) frontmatter["metro"] = skillset.Metro.ToList();

        var yaml = Serializer.Serialize(frontmatter).TrimEnd();

        var sb = new StringBuilder();
        sb.Append("---\n");
        sb.Append(yaml);
        sb.Append("\n---\n\n");
        AppendSection(sb, PrimarySection, skillset.PrimaryStack, "Must-have. A listing that hits multiple of these scores high.");
        AppendSection(sb, SecondarySection, skillset.SecondaryStack, "Nice-to-have. Bumps the score modestly.");
        AppendSection(sb, DomainsSection, skillset.Domains, "Industries or problem spaces to target.");
        AppendSection(sb, DisqualifiersSection, skillset.Disqualifiers, "Listings matching any of these drop to score 0.");
        return sb.ToString();
    }

    public static void Save(Skillset skillset, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Serialize(skillset));
    }

    private static IReadOnlyDictionary<string, object?> DeserializeYaml(string yaml)
    {
        var raw = Deserializer.Deserialize<Dictionary<object, object?>>(yaml);
        if (raw is null) return new Dictionary<string, object?>();
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kvp in raw)
        {
            var key = kvp.Key?.ToString();
            if (!string.IsNullOrEmpty(key)) result[key] = kvp.Value;
        }
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseSections(string body)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? current = null;

        foreach (var raw in body.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                current = line[3..].Trim();
                if (!sections.ContainsKey(current)) sections[current] = new List<string>();
            }
            else if (current != null)
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
                {
                    var item = trimmed[2..].Trim();
                    if (item.Length > 0) sections[current].Add(item);
                }
            }
        }

        return sections.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<string>)kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static Skillset Build(IReadOnlyDictionary<string, object?> yaml, IReadOnlyDictionary<string, IReadOnlyList<string>> sections)
    {
        var name = RequireString(yaml, "name");
        var location = RequireString(yaml, "location");
        var experienceYears = RequireInt(yaml, "experience_years");
        var targetRoles = ReadList(yaml, "target_roles");
        var remotePref = RequireEnum<RemotePreference>(yaml, "remote_preference");
        var seniority = RequireEnum<Seniority>(yaml, "seniority");
        var languages = ReadList(yaml, "languages");
        var employmentTypes = ReadList(yaml, "employment_types", ["full-time"]);
        var country = OptionalString(yaml, "country");
        var region = OptionalString(yaml, "region");
        var metro = ReadList(yaml, "metro");

        return new Skillset(
            Name: name,
            Location: location,
            ExperienceYears: experienceYears,
            TargetRoles: targetRoles,
            RemotePreference: remotePref,
            Seniority: seniority,
            PrimaryStack: ReadSection(sections, PrimarySection),
            SecondaryStack: ReadSection(sections, SecondarySection),
            Domains: ReadSection(sections, DomainsSection),
            Disqualifiers: ReadSection(sections, DisqualifiersSection),
            Languages: languages,
            EmploymentTypes: employmentTypes)
        {
            Country = country,
            Region = region,
            Metro = metro,
        };
    }

    private static string? OptionalString(IReadOnlyDictionary<string, object?> yaml, string key)
    {
        if (!yaml.TryGetValue(key, out var v) || v is null) return null;
        var s = v.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static string RequireString(IReadOnlyDictionary<string, object?> yaml, string key)
    {
        if (!yaml.TryGetValue(key, out var v) || v is null)
        {
            throw new ConfigException($"skillset frontmatter: missing required field '{key}'");
        }
        var s = v.ToString();
        if (string.IsNullOrWhiteSpace(s))
        {
            throw new ConfigException($"skillset frontmatter: field '{key}' must not be empty");
        }
        return s;
    }

    private static int RequireInt(IReadOnlyDictionary<string, object?> yaml, string key)
    {
        var s = RequireString(yaml, key);
        if (!int.TryParse(s, out var n))
        {
            throw new ConfigException($"skillset frontmatter: '{key}' must be an integer, got '{s}'");
        }
        return n;
    }

    private static T RequireEnum<T>(IReadOnlyDictionary<string, object?> yaml, string key) where T : struct, Enum
    {
        var s = RequireString(yaml, key);
        if (!Enum.TryParse<T>(s, ignoreCase: true, out var v))
        {
            throw new ConfigException($"skillset frontmatter: '{key}' must be one of [{string.Join(", ", Enum.GetNames<T>()).ToLowerInvariant()}], got '{s}'");
        }
        return v;
    }

    private static IReadOnlyList<string> ReadList(IReadOnlyDictionary<string, object?> yaml, string key, IReadOnlyList<string>? defaultValue = null)
    {
        if (!yaml.TryGetValue(key, out var v) || v is null)
        {
            return defaultValue ?? [];
        }
        if (v is IEnumerable<object?> seq)
        {
            return seq.Select(x => x?.ToString() ?? string.Empty).Where(s => s.Length > 0).ToList();
        }
        var scalar = v.ToString();
        return string.IsNullOrEmpty(scalar) ? [] : [scalar];
    }

    private static IReadOnlyList<string> ReadSection(IReadOnlyDictionary<string, IReadOnlyList<string>> sections, string name) =>
        sections.TryGetValue(name, out var items) ? items : [];

    private static void AppendSection(StringBuilder sb, string heading, IReadOnlyList<string> items, string lead)
    {
        sb.Append("## ").Append(heading).Append('\n');
        sb.Append(lead).Append("\n\n");
        if (items.Count == 0)
        {
            sb.Append('\n');
            return;
        }
        foreach (var item in items)
        {
            sb.Append("- ").Append(item).Append('\n');
        }
        sb.Append('\n');
    }
}
