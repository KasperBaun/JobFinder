using Jobmatch.Domain;

namespace Jobmatch.Features.Skillsets;

public interface ISkillsetService
{
    Skillset Get();

    /// <summary>Like <see cref="Get"/> but null instead of throwing when no profile exists yet.</summary>
    Skillset? Find();

    /// <summary>
    /// Writes the profile, resolving the address to coordinates when it changed (R-105). Coordinates
    /// are never client input — the service owns them, so a caller cannot save a profile whose
    /// stored position disagrees with its address.
    /// </summary>
    Task<Skillset> UpdateAsync(SkillsetUpdate input, CancellationToken ct = default);
}
