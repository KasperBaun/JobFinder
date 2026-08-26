using System.Text;

namespace Jobmatch.Features.Drafting;

/// <summary>
/// Names drafted files so a user scanning <c>documents/</c> can tell them apart, and so nothing a
/// job ad contains can escape that directory. The listing id suffix keeps two applications to the
/// same role at the same company from colliding.
/// </summary>
public static class DraftFileNames
{
    private const int MaxStemLength = 60;

    public static string Resume(ApplicationDraft draft, string listingId)
        => $"{Stem(draft, listingId)}_Resume.docx";

    public static string CoverLetter(ApplicationDraft draft, string listingId)
        => $"{Stem(draft, listingId)}_CoverLetter.docx";

    internal static string Stem(ApplicationDraft draft, string listingId)
    {
        var company = Sanitize(draft.CompanyName, "Company");
        var role = Sanitize(draft.JobTitle, "Role");
        var suffix = Sanitize(listingId, "id");
        suffix = suffix.Length <= 8 ? suffix : suffix[..8];
        return $"{company}_{role}_{suffix}";
    }

    private static string Sanitize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        var sb = new StringBuilder(value.Length);
        var lastWasSeparator = false;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                sb.Append('_');
                lastWasSeparator = true;
            }
        }

        var cleaned = sb.ToString().Trim('_');
        if (cleaned.Length == 0) return fallback;
        return cleaned.Length <= MaxStemLength ? cleaned : cleaned[..MaxStemLength].TrimEnd('_');
    }
}
