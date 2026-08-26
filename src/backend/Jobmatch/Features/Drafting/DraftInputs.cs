using Jobmatch.Domain;

namespace Jobmatch.Features.Drafting;

/// <summary>
/// Everything the writer needs, already resolved. Separating this from the service keeps prompt
/// construction testable without a filesystem or a model.
/// </summary>
public sealed record DraftInputs(string CvText, Skillset? Skillset, string JobAdText);
