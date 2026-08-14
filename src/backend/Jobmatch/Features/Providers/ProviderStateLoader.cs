using System.Text.Json.Serialization;
using System.Text.Json;
using Jobmatch.Platform.IO;
using Jobmatch.Platform.Json;

namespace Jobmatch.Features.Providers;

public static class ProviderStateLoader
{
    private static readonly JsonSerializerOptions SerializeOptions = JobmatchJsonOptions.Indented;

    // Reads stay case-insensitive: state files written before the camelCase policy existed
    // carry PascalCase members, and this file is the user's provider opt-in/opt-out — losing it
    // to a casing mismatch would silently re-enable every source they turned off.
    private static readonly JsonSerializerOptions DeserializeOptions =
        new(JobmatchJsonOptions.Default) { PropertyNameCaseInsensitive = true };

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

        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(raw, SerializeOptions));
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
