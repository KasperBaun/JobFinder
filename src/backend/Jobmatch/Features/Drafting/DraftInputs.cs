using Jobmatch.Domain;

namespace Jobmatch.Features.Drafting;

/// <summary>
/// Everything the writer needs, already resolved. Separating this from the service keeps prompt
/// construction testable without a filesystem or a model. The role and employer come from the
/// listing record rather than from the ad text, so the model is told what it is applying for instead
/// of being asked to read it back out of scraped markup.
/// </summary>
public sealed record DraftInputs(
    string CvText,
    Skillset? Skillset,
    string JobTitle,
    string? CompanyName,
    string JobAdText);
