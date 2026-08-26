namespace Jobmatch.Features.Drafting;

public interface IApplicationDraftService
{
    /// <summary>
    /// Drafts a resume and cover letter for one listing and writes both to <c>documents/</c>.
    /// </summary>
    Task<DraftedDocuments> DraftAsync(string runId, string listingId, CancellationToken ct = default);
}
