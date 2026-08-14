using Jobmatch.Models;

namespace Jobmatch.Services;

public interface ISkillsetService
{
    Skillset Get();

    /// <summary>Like <see cref="Get"/> but null instead of throwing when no profile exists yet.</summary>
    Skillset? Find();

    Skillset Update(SkillsetUpdate input);
}
