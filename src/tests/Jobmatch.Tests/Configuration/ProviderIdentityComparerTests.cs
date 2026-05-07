using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class ProviderIdentityComparerTests
{
    private static PortalConfig Make(int id, string name) =>
        new(Name: name, Type: PortalType.Manual, Id: id);

    [Fact]
    public void Equal_When_Same_Positive_Id()
    {
        var a = Make(7, "x");
        var b = Make(7, "y-renamed");
        Assert.True(ProviderIdentityComparer.Instance.Equals(a, b));
    }

    [Fact]
    public void NotEqual_When_Different_Ids()
    {
        var a = Make(1, "x");
        var b = Make(2, "x");
        Assert.False(ProviderIdentityComparer.Instance.Equals(a, b));
    }

    [Fact]
    public void NotEqual_When_Either_Id_Is_Zero()
    {
        var a = Make(0, "x");
        var b = Make(0, "x");
        Assert.False(ProviderIdentityComparer.Instance.Equals(a, b));
    }
}
