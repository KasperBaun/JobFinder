namespace Jobmatch.Search.Locations;

/// <summary>One row of the bundled gazetteer: a place plus the lookup aliases it answers to.</summary>
public sealed record GazetteerEntry(
    string Name,
    IReadOnlyList<string> Aliases,
    double Latitude,
    double Longitude,
    string CountryCode,
    GeoPlaceType Type,
    long Population)
{
    public GeoPlace ToPlace() => new(Name, Latitude, Longitude, CountryCode, Type);
}
