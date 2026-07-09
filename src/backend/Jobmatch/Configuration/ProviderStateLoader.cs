using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jobmatch.Configuration;

public static class ProviderStateLoader
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ProviderState LoadOrEmpty(string path)
    {
        if (!File.Exists(path))
            return ProviderState.Empty;

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
            return ProviderState.Empty;

        var raw = JsonSerializer.Deserialize<RawProviderState>(json, DeserializeOptions);
        if (raw is null)
            return ProviderState.Empty;

        var disabled = (IReadOnlyList<int>)(raw.Disabled ?? Array.Empty<int>());
        var enabled = (IReadOnlyList<int>)(raw.Enabled ?? Array.Empty<int>());

        var secrets = new Dictionary<int, IReadOnlyDictionary<string, string>>();
        if (raw.Secrets is not null)
        {
            foreach (var (key, value) in raw.Secrets)
            {
                if (int.TryParse(key, out var id) && value is not null)
                    secrets[id] = value;
            }
        }

        var overrides = new Dictionary<int, ProviderOverride>();
        if (raw.Overrides is not null)
        {
            foreach (var (key, value) in raw.Overrides)
            {
                if (int.TryParse(key, out var id) && value is not null && !value.IsEmpty)
                    overrides[id] = value;
            }
        }

        return new ProviderState(disabled, enabled, secrets, overrides);
    }

    public static void Save(string path, ProviderState state)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        var raw = new RawProviderState(
            state.Disabled.ToArray(),
            state.Enabled.ToArray(),
            state.Secrets.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value.ToDictionary(s => s.Key, s => s.Value)),
            state.Overrides
                .Where(kvp => !kvp.Value.IsEmpty)
                .ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));

        var json = JsonSerializer.Serialize(raw, SerializeOptions);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    private sealed class RawProviderState
    {
        public RawProviderState(
            int[]? disabled,
            int[]? enabled,
            Dictionary<string, Dictionary<string, string>>? secrets,
            Dictionary<string, ProviderOverride>? overrides)
        {
            Disabled = disabled;
            Enabled = enabled;
            Secrets = secrets;
            Overrides = overrides;
        }

        [JsonPropertyName("disabled")]
        public int[]? Disabled { get; }

        [JsonPropertyName("enabled")]
        public int[]? Enabled { get; }

        [JsonPropertyName("secrets")]
        public Dictionary<string, Dictionary<string, string>>? Secrets { get; }

        [JsonPropertyName("overrides")]
        public Dictionary<string, ProviderOverride>? Overrides { get; }
    }
}
