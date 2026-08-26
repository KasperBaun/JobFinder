using Jobmatch.Api.Infrastructure;
using Jobmatch.Domain;
using Jobmatch.Features.Skillsets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Jobmatch.Api.Contracts;

namespace Jobmatch.Api.Features.Skillsets;

public interface ISkillsetHandler
{
    Task<IResult> Get();
    Task<IResult> Update(SkillsetUpdateRequest? request);
}

public sealed class SkillsetHandler(
    ISkillsetService skillset,
    ILogger<SkillsetHandler> logger)
    : HandlerBase(logger), ISkillsetHandler
{
    public Task<IResult> Get() => ExecuteAsync(
        "get skillset",
        () => Task.FromResult<IResult>(Results.Ok(ToResponse(skillset.Get()))));

    public Task<IResult> Update(SkillsetUpdateRequest? request) => ExecuteAsync(
        "update skillset",
        async () =>
        {
            if (request is null)
                throw new InvalidRequestException("request body is required");

            await skillset.UpdateAsync(ToUpdate(request));
            return Results.Ok(new SaveResponse(true));
        });

    private static SkillsetUpdate ToUpdate(SkillsetUpdateRequest r) => new(
        Name: r.Name,
        Location: r.Location,
        ExperienceYears: r.ExperienceYears,
        TargetRoles: r.TargetRoles,
        RemotePreference: r.RemotePreference,
        Seniority: r.Seniority,
        PrimaryStack: r.PrimaryStack,
        SecondaryStack: r.SecondaryStack,
        Domains: r.Domains,
        Disqualifiers: r.Disqualifiers,
        Languages: r.Languages,
        EmploymentTypes: r.EmploymentTypes,
        Country: r.Country,
        Region: r.Region,
        Metro: r.Metro,
        PreferredCompanies: r.PreferredCompanies,
        Address: r.Address,
        RadiusKm: r.RadiusKm);

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
