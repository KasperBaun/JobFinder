using Jobmatch.Domain;

namespace Jobmatch.Pipeline.Geo;

/// <summary>What the radius filter decided for one listing that sits too far out.</summary>
public sealed record RadiusVerdict(int Km, int MaxKm, string Place);

/// <summary>
/// Hard spatial filter around the user's geocoded home address (R-105). Active only when
/// the skillset carries coordinates and a positive radius — <see cref="Create"/> returns
/// null otherwise. Distance is measured to the nearest *site* the location names (see
/// <see cref="Gazetteer.ResolveSites"/>), so a country mentioned alongside a city qualifies
/// that city rather than competing with it. Fully remote listings are exempt; listings whose
/// location is missing or unresolvable pass through undropped. Offline by construction:
/// distances come from the bundled gazetteer, never a network call.
/// </summary>
public sealed class RadiusFilter
{
    private readonly Gazetteer _gazetteer;
    private readonly double _homeLatitude;
    private readonly double _homeLongitude;
    private readonly double _radiusKm;
    private readonly string? _homeCountryCode;

    private RadiusFilter(Gazetteer gazetteer, double latitude, double longitude, double radiusKm, string? homeCountryCode)
    {
        _gazetteer = gazetteer;
        _homeLatitude = latitude;
        _homeLongitude = longitude;
        _radiusKm = radiusKm;
        _homeCountryCode = homeCountryCode;
    }

    public static RadiusFilter? Create(Skillset skillset, Gazetteer gazetteer)
    {
        if (skillset.Latitude is not double lat || skillset.Longitude is not double lon
            || skillset.RadiusKm is not > 0)
        {
            return null;
        }
        return new RadiusFilter(gazetteer, lat, lon, skillset.RadiusKm.Value, HomeCountryCode(skillset, gazetteer));
    }

    /// <summary>Null = keep (exempt, unresolvable, or within radius); a verdict = drop.</summary>
    public RadiusVerdict? Evaluate(Listing listing)
    {
        if (listing.RemoteMode == RemoteMode.Remote) return null;

        var places = _gazetteer.ResolveSites(listing.Location, _homeCountryCode);
        if (places.Count == 0) return null;

        // "Region Hovedstaden" or a bare "Danmark" names an area the user may well live inside;
        // its stored point is a centroid or the capital, so measuring it would hide a job that is
        // next door. Own-country areas say which country, not where — treat them as unstated and
        // fall through undropped, the same as any location the gazetteer can't place.
        if (places.Any(IsHomeArea)) return null;

        var nearest = places[0];
        var minKm = double.MaxValue;
        foreach (var place in places)
        {
            var km = GeoDistance.HaversineKm(_homeLatitude, _homeLongitude, place.Latitude, place.Longitude);
            if (km < minKm)
            {
                minKm = km;
                nearest = place;
            }
        }

        // Rounded up, not to nearest: a site 44.4 km out of a 44 km radius must not report
        // "~44 km away, max 44 km" and read like the filter contradicted itself.
        return minKm <= _radiusKm
            ? null
            : new RadiusVerdict((int)Math.Ceiling(minKm), (int)Math.Round(_radiusKm), nearest.Name);
    }

    private bool IsHomeArea(GeoPlace place) =>
        place.Type is GeoPlaceType.Region or GeoPlaceType.Country
        && string.Equals(place.CountryCode, _homeCountryCode, StringComparison.OrdinalIgnoreCase);

    // Scopes ambiguous names ("Odense" in two countries) to the user's country. The
    // coordinates only ever come from the DK-only DAWA geocoder, so DK is the fallback
    // when neither the country field nor the location string resolves.
    private static string? HomeCountryCode(Skillset skillset, Gazetteer gazetteer) =>
        gazetteer.Resolve(skillset.Country, null)?.CountryCode
        ?? gazetteer.Resolve(skillset.Location, null)?.CountryCode
        ?? "DK";
}
