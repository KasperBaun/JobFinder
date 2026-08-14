using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Domain;
using Jobmatch.Features.Skillsets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface ISkillsetHandler
{
    Task<IResult> Get();
    Task<IResult> Update(SkillsetUpdateRequest? request);
}

public sealed class SkillsetHandler(
    ISkillsetService skillset,
    IGeocodingService geocoding,
    ILogger<SkillsetHandler> logger)
    : HandlerBase(logger), ISkillsetHandler
{
    public Task<IResult> Get() => ExecuteAsync(
        "get skillset",
        () =>
        {
            var s = skillset.Get();
            return Task.FromResult<IResult>(Results.Ok(ToResponse(s)));
        });

    public Task<IResult> Update(SkillsetUpdateRequest? request) => ExecuteAsync(
        "update skillset",
        async () =>
        {
            if (request is null)
                throw new InvalidRequestException("request body is required");

            var geocode = await ResolveGeocodeAsync(request);
            var input = new SkillsetUpdate(
                Name: request.Name,
                Location: request.Location,
                ExperienceYears: request.ExperienceYears,
                TargetRoles: request.TargetRoles,
                RemotePreference: request.RemotePreference,
                Seniority: request.Seniority,
                PrimaryStack: request.PrimaryStack,
                SecondaryStack: request.SecondaryStack,
                Domains: request.Domains,
                Disqualifiers: request.Disqualifiers,
                Languages: request.Languages,
                EmploymentTypes: request.EmploymentTypes,
                Country: request.Country,
                Region: request.Region,
                Metro: request.Metro,
                PreferredCompanies: request.PreferredCompanies,
                Address: request.Address,
                RadiusKm: request.RadiusKm,
                Latitude: geocode?.Latitude,
                Longitude: geocode?.Longitude,
                ResolvedAddress: geocode?.ResolvedAddress);

            skillset.Update(input);
            return Results.Ok(new SaveResponse(true));
        });

    // Geocode only when the address is new or was never resolved; an unchanged address
    // keeps its stored coordinates without a network call, and a blank address clears
    // everything. A failed geocode still saves — coordinates just stay empty (R-105).
    private async Task<GeocodeResult?> ResolveGeocodeAsync(SkillsetUpdateRequest request)
    {
        var address = request.Address?.Trim();
        if (string.IsNullOrEmpty(address)) return null;

        var existing = skillset.Find();
        if (existing is { Latitude: double lat, Longitude: double lon }
            && string.Equals(existing.Address, address, StringComparison.Ordinal))
        {
            return new GeocodeResult(lat, lon, existing.ResolvedAddress ?? address);
        }
        return await geocoding.GeocodeAsync(address);
    }

    private static SkillsetResponse ToResponse(Skillset s) => new(
        Name: s.Name,
        Location: s.Location,
        ExperienceYears: s.ExperienceYears,
        TargetRoles: s.TargetRoles,
        RemotePreference: s.RemotePreference.ToString().ToLowerInvariant(),
        Seniority: s.Seniority.ToString().ToLowerInvariant(),
        PrimaryStack: s.PrimaryStack,
        SecondaryStack: s.SecondaryStack,
        Domains: s.Domains,
        Disqualifiers: s.Disqualifiers,
        Languages: s.Languages,
        EmploymentTypes: s.EmploymentTypes,
        Country: s.Country,
        Region: s.Region,
        Metro: s.Metro,
        PreferredCompanies: s.PreferredCompanies,
        Address: s.Address,
        RadiusKm: s.RadiusKm,
        Latitude: s.Latitude,
        Longitude: s.Longitude,
        ResolvedAddress: s.ResolvedAddress);
}
