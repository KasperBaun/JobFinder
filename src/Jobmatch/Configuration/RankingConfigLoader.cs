using Jobmatch.Models;
using YamlDotNet.Serialization;

namespace Jobmatch.Configuration;

public static class RankingConfigLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    public static RankingConfig Parse(string yaml)
    {
        Dictionary<object, object?>? root;
        try
        {
            root = Deserializer.Deserialize<Dictionary<object, object?>>(yaml);
        }
        catch (Exception ex) when (ex is not ConfigException)
        {
            throw new ConfigException($"ranking: YAML parse error — {ex.Message}", ex);
        }

        if (root is null)
        {
            throw new ConfigException("ranking: empty config");
        }

        var map = Normalise(root);

        if (!map.TryGetValue("weights", out var weightsRaw) || weightsRaw is not IDictionary<object, object?> weightsMap)
        {
            throw new ConfigException("ranking: missing 'weights' mapping");
        }

        var weights = BuildWeights(Normalise(weightsMap));
        var disqualifierPenalty = ReadDouble(map, "disqualifier_penalty", 0.0);
        var topN = ReadInt(map, "top_n", 10);
        var halfLife = ReadDouble(map, "freshness_half_life_days", 14.0);
        var minScore = ReadDouble(map, "min_score_to_include", 0.25);

        return new RankingConfig(weights, disqualifierPenalty, topN, halfLife, minScore);
    }

    public static RankingConfig Load(string path) => Parse(File.ReadAllText(path));

    private static RankingWeights BuildWeights(IReadOnlyDictionary<string, object?> weights) =>
        new(
            PrimaryStack: ReadDouble(weights, "primary_stack", 0.0),
            SecondaryStack: ReadDouble(weights, "secondary_stack", 0.0),
            Seniority: ReadDouble(weights, "seniority", 0.0),
            LocationRemote: ReadDouble(weights, "location_remote", 0.0),
            Domain: ReadDouble(weights, "domain", 0.0),
            Freshness: ReadDouble(weights, "freshness", 0.0));

    private static IReadOnlyDictionary<string, object?> Normalise(IDictionary<object, object?> map)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kvp in map)
        {
            var k = kvp.Key?.ToString();
            if (!string.IsNullOrEmpty(k)) result[k] = kvp.Value;
        }
        return result;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, object?> map, string key, double defaultValue)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return defaultValue;
        return double.TryParse(v.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : defaultValue;
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> map, string key, int defaultValue)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return defaultValue;
        return int.TryParse(v.ToString(), out var n) ? n : defaultValue;
    }
}
