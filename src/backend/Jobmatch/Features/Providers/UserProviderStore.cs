using System.Text.Json.Serialization;
using System.Text.Json;
using Jobmatch.Infrastructure.IO;
using Jobmatch.Infrastructure.Json;

namespace Jobmatch.Features.Providers;

/// <summary>
/// Per-user, writable catalog of provider entries the user added themselves
/// (<c>data/&lt;email&gt;/user-providers.json</c>). Stored in the same
/// <c>{ "version": 1, "providers": [ … ] }</c> shape as the shipped catalog and read back
/// through <see cref="PortalCatalogLoader.Parse"/> so both paths share one parser and its
/// validation. Ids live at or above <see cref="IdBase"/> so they never collide with the
/// baked catalog (ids 1–49).
/// </summary>
public static class UserProviderStore
{
    public const int IdBase = 10000;

    private static readonly JsonSerializerOptions WriteOptions = JobmatchJsonOptions.Indented;

    public static IReadOnlyList<PortalConfig> Load(string path)
    {
        if (!File.Exists(path)) return [];
        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return PortalCatalogLoader.Parse(json);
    }

    public static void Save(string path, IReadOnlyList<PortalConfig> providers)
    {
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(new { version = 1, providers }, WriteOptions));
    }

    /// <summary>
    /// Appends <paramref name="draft"/> as a new user provider, assigning it the next free id and
    /// validating that its name/endpoint don't collide with any existing provider (baked or user).
    /// Returns the persisted record (with its assigned id).
    /// </summary>
    public static PortalConfig Add(
        string path,
        PortalConfig draft,
        IReadOnlyList<PortalConfig> catalog)
    {
        var existing = Load(path);
        var created = draft with { Id = NextId(existing) };

        ProviderListValidator.AssertNotDuplicate(created, [.. catalog, .. existing]);

        Save(path, [.. existing, created]);
        return created;
    }

    public static bool Remove(string path, int id)
    {
        var existing = Load(path);
        var remaining = existing.Where(p => p.Id != id).ToList();
        if (remaining.Count == existing.Count) return false;
        Save(path, remaining);
        return true;
    }

    private static int NextId(IReadOnlyList<PortalConfig> existing) =>
        existing.Count == 0 ? IdBase : Math.Max(IdBase, existing.Max(p => p.Id) + 1);
}
