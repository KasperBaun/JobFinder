namespace Jobmatch.Configuration;

public sealed record ProviderState(
    IReadOnlyList<int> Disabled,
    IReadOnlyList<int> Enabled,
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> Secrets,
    IReadOnlyDictionary<int, ProviderOverride> Overrides)
{
    public static ProviderState Empty { get; } = new(
        Array.Empty<int>(),
        Array.Empty<int>(),
        new Dictionary<int, IReadOnlyDictionary<string, string>>(),
        new Dictionary<int, ProviderOverride>());
}
