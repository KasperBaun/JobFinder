using Jobmatch.Domain;

namespace Jobmatch.Features.Skillsets;

public interface ISkillsetService
{
    Skillset Get();

    /// <summary>Like <see cref="Get"/> but null instead of throwing when no profile exists yet.</summary>
    Skillset? Find();

    Skillset Update(SkillsetUpdate input);
}
