using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Models;
using Jobmatch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface ISkillsetHandler
{
    Task<IResult> Get();
    Task<IResult> Update(SkillsetUpdateRequest? request);
}

public sealed class SkillsetHandler(ISkillsetService skillset, ILogger<SkillsetHandler> logger)
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
        () =>
        {
            if (request is null)
                throw new InvalidRequestException("request body is required");

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
                PreferredCompanies: request.PreferredCompanies);

            skillset.Update(input);
            return Task.FromResult<IResult>(Results.Ok(new SaveResponse(true)));
        });

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
        PreferredCompanies: s.PreferredCompanies);
}
