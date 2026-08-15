namespace Jobmatch.Search.Locations;

/// <summary>Declared in specificity order — a lower value wins when one location string
/// matches entries of several types (postal beats city beats region beats country).</summary>
public enum GeoPlaceType { Postal, City, Region, Country }

public sealed record GeoPlace(
    string Name,
    double Latitude,
    double Longitude,
    string CountryCode,
    GeoPlaceType Type);
