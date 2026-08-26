namespace Jobmatch.Features.Cv;

/// <summary>
/// The user's CV text, kept as <c>cv.md</c> so drafting has the career facts the skillset does not
/// carry — employment history, education, contact details. Written whenever a CV is extracted, and
/// editable on its own for users who never ran an extraction.
/// </summary>
public interface ICvDocumentStore
{
    /// <summary>The stored CV text, or null when the user has none yet.</summary>
    string? Find();

    void Save(string text);
}
