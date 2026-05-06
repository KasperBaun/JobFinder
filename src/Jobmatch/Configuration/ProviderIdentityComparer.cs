using Jobmatch.Models;

namespace Jobmatch.Configuration;

/// <summary>
/// Identity equality for <see cref="PortalConfig"/> records — two records are the same record
/// iff they share a positive <see cref="PortalConfig.Id"/>. Use for collection ops that mean
/// "find this provider," not for duplicate detection (see <see cref="ProviderListValidator"/>).
/// </summary>
public sealed class ProviderIdentityComparer : IEqualityComparer<PortalConfig>
{
    public static readonly ProviderIdentityComparer Instance = new();

    public bool Equals(PortalConfig? x, PortalConfig? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Id <= 0 || y.Id <= 0) return false;
        return x.Id == y.Id;
    }

    public int GetHashCode(PortalConfig obj) => obj.Id.GetHashCode();
}
