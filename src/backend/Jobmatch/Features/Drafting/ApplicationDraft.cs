namespace Jobmatch.Features.Drafting;

/// <summary>
/// What the model wrote for one listing. Markdown rather than a document format because the model
/// produces text and every export reads from here — the .docx files are a rendering of this, not a
/// second source of truth.
/// </summary>
public sealed record ApplicationDraft(
    string JobTitle,
    string CompanyName,
    string ResumeMarkdown,
    string CoverLetterMarkdown);
