using Jobmatch.Configuration;
using Jobmatch.Gui.Server.Models;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Gui.Server.Handlers;

public static class SkillsetHandler
{
    public static IResult Get(Jobmatch.UserContext ctx)
    {
        try
        {
            var s = SkillsetParser.Load(ctx.SkillsetPath);
            return Results.Ok(new SkillsetResponse(
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
                EmploymentTypes: s.EmploymentTypes));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
