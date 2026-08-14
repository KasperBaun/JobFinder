using Jobmatch.Features.Skillsets;

namespace Jobmatch.Tests;

/// <summary>A geocoder that resolves nothing — for tests about anything other than geocoding.
/// Profiles still save; they just carry no coordinates (R-105).</summary>
internal sealed class NullGeocoder : IGeocodingService
{
    public Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken ct = default) =>
        Task.FromResult<GeocodeResult?>(null);
}
