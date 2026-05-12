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
        var maxAgeDays = ReadNullableInt(map, "max_age_days");
        var requirePrimaryStackHit = ReadBool(map, "require_primary_stack_hit", false);
        var seniorityAdjacencyCredit = ReadDouble(map, "seniority_adjacency_credit", 1.0);
        var nonEngineeringTitleMultiplier = ReadDouble(map, "non_engineering_title_multiplier", 0.2);
        var tierWeights = BuildTierWeights(map);
        var llm = BuildLlmConfig(map);

        return new RankingConfig(
            weights, disqualifierPenalty, topN, halfLife, minScore,
            maxAgeDays, requirePrimaryStackHit,
            seniorityAdjacencyCredit, nonEngineeringTitleMultiplier)
        {
            LocationTierWeights = tierWeights,
            Llm = llm,
        };
    }

    private static LlmConfig BuildLlmConfig(IReadOnlyDictionary<string, object?> root)
    {
        if (!root.TryGetValue("llm", out var raw) || raw is not IDictionary<object, object?> map)
            return LlmConfig.Disabled;
        var n = Normalise(map);
        var d = LlmConfig.Disabled;
        return new LlmConfig(
            Enabled: ReadBool(n, "enabled", d.Enabled),
            Provider: ReadString(n, "provider", d.Provider),
            Model: ReadString(n, "model", d.Model),
            ModelPath: ReadString(n, "model_path", d.ModelPath),
            BaseUrl: ReadString(n, "base_url", d.BaseUrl),
            TopN: ReadInt(n, "top_n", d.TopN),
            Weight: ReadDouble(n, "weight", d.Weight),
            Temperature: ReadDouble(n, "temperature", d.Temperature),
            ContextSize: ReadInt(n, "context_size", d.ContextSize),
            GpuLayerCount: ReadInt(n, "gpu_layer_count", d.GpuLayerCount));
    }

    private static string ReadString(IReadOnlyDictionary<string, object?> map, string key, string defaultValue)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return defaultValue;
        var s = v.ToString();
        return string.IsNullOrWhiteSpace(s) ? defaultValue : s;
    }

    private static LocationTierWeights BuildTierWeights(IReadOnlyDictionary<string, object?> root)
    {
        if (!root.TryGetValue("location_tier_weights", out var raw) || raw is not IDictionary<object, object?> map)
            return LocationTierWeights.Default;
        var n = Normalise(map);
        var d = LocationTierWeights.Default;
        return new LocationTierWeights(
            City: ReadDouble(n, "city", d.City),
            Metro: ReadDouble(n, "metro", d.Metro),
            Country: ReadDouble(n, "country", d.Country),
            Region: ReadDouble(n, "region", d.Region),
            Else: ReadDouble(n, "else", d.Else));
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

    private static int? ReadNullableInt(IReadOnlyDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return null;
        var s = v.ToString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        return int.TryParse(s, out var n) ? n : null;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> map, string key, bool defaultValue)
    {
        if (!map.TryGetValue(key, out var v) || v is null) return defaultValue;
        return bool.TryParse(v.ToString(), out var b) ? b : defaultValue;
    }
}
