using Jobmatch.Geo;

namespace Jobmatch.Tests.Geo;

public sealed class GeoDistanceTests
{
    private const double CphLat = 55.6761, CphLon = 12.5683;
    private const double AarhusLat = 56.1567, AarhusLon = 10.2108;

    [Fact]
    public void HaversineKm_Copenhagen_To_Aarhus_Is_About_157()
    {
        var km = GeoDistance.HaversineKm(CphLat, CphLon, AarhusLat, AarhusLon);
        Assert.InRange(km, 154, 160);
    }

    [Fact]
    public void HaversineKm_Copenhagen_To_Warsaw_Is_About_668()
    {
        var km = GeoDistance.HaversineKm(CphLat, CphLon, 52.2298, 21.0118);
        Assert.InRange(km, 660, 680);
    }

    [Fact]
    public void HaversineKm_Same_Point_Is_Zero()
    {
        Assert.Equal(0.0, GeoDistance.HaversineKm(CphLat, CphLon, CphLat, CphLon), precision: 6);
    }

    [Fact]
    public void HaversineKm_Is_Symmetric()
    {
        var ab = GeoDistance.HaversineKm(CphLat, CphLon, AarhusLat, AarhusLon);
        var ba = GeoDistance.HaversineKm(AarhusLat, AarhusLon, CphLat, CphLon);
        Assert.Equal(ab, ba, precision: 9);
    }
}
