using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Services;

/// <summary>
/// Geocodes Danish addresses against the public DAWA address API. Any failure — not
/// found, offline, timeout, non-2xx, bad payload — returns null so the profile save
/// always succeeds and the radius filter simply stays inactive (R-105).
/// </summary>
public sealed class DawaGeocodingService(HttpClient http, ILogger<DawaGeocodingService> logger) : IGeocodingService
{
    private const string BaseUrl = "https://api.dataforsyningen.dk";

    public async Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken ct = default)
    {
        try
        {
            // Full-address search first; access-address fallback catches inputs without
            // a floor/door suffix that the primary index doesn't return.
            return await QueryAsync("adresser", address, ct).ConfigureAwait(false)
                ?? await QueryAsync("adgangsadresser", address, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Geocoding failed; saving the profile without coordinates");
            return null;
        }
    }

    private async Task<GeocodeResult?> QueryAsync(string resource, string address, CancellationToken ct)
    {
        var url = $"{BaseUrl}/{resource}?q={Uri.EscapeDataString(address)}&struktur=mini&per_side=1";
        using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return null;
        }
        var row = doc.RootElement[0];
        // In DAWA's mini struktur, x is the LONGITUDE and y the LATITUDE.
        return new GeocodeResult(
            Latitude: row.GetProperty("y").GetDouble(),
            Longitude: row.GetProperty("x").GetDouble(),
            ResolvedAddress: row.GetProperty("betegnelse").GetString() ?? address);
    }
}
