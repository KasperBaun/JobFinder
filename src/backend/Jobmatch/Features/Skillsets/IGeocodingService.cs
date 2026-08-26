namespace Jobmatch.Features.Skillsets;

public sealed record GeocodeResult(double Latitude, double Longitude, string ResolvedAddress);

/// <summary>Resolves a street address to coordinates once, at profile-save time —
/// never at rank time (R-105). Null means "could not resolve"; a save proceeds anyway.</summary>
public interface IGeocodingService
{
    Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken ct = default);
}
