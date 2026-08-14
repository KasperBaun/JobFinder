namespace Jobmatch.Features.Providers;

public interface ISourceDetectionService
{
    /// <summary>Pattern-matches a pasted URL to known ATS boards or an RSS feed. Pure; no network.</summary>
    IReadOnlyList<SourceCandidate> Detect(Uri url);

    /// <summary>Builds a manual-import source from a user-supplied name (no endpoint).</summary>
    SourceCandidate BuildManual(string displayName);
}
