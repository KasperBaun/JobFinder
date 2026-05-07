namespace Jobmatch.Configuration;

public sealed record ProviderState(
    IReadOnlyList<int> Disabled,
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> Secrets)
{
    public static ProviderState Empty { get; } = new(
        Array.Empty<int>(),
        new Dictionary<int, IReadOnlyDictionary<string, string>>());
}
