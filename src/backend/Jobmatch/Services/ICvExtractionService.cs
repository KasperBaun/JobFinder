using Jobmatch.Models;

namespace Jobmatch.Services;

public interface ICvExtractionService
{
    Task<ExtractedProfile> ExtractAsync(CvSource source, CancellationToken ct = default);
}
