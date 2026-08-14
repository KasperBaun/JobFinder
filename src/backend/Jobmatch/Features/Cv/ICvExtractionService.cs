namespace Jobmatch.Features.Cv;

public interface ICvExtractionService
{
    Task<ExtractedProfile> ExtractAsync(CvSource source, CancellationToken ct = default);
}
