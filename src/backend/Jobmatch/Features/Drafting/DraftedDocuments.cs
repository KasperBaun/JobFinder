namespace Jobmatch.Features.Drafting;

/// <summary>The draft plus where it was written under <c>documents/</c>.</summary>
public sealed record DraftedDocuments(
    string ListingId,
    string RunId,
    ApplicationDraft Draft,
    string ResumePath,
    string CoverLetterPath,
    DateTimeOffset DraftedAt);
