using System.Text.RegularExpressions;

namespace Jobmatch.Cv;

// Normalizes extracted CV text before it goes to the LLM: collapse whitespace,
// then truncate head-first to fit the model's context window. CVs front-load
// identity, summary and skills, so the tail is the safest cut.
public static partial class CvTextNormalizer
{
    public const int MaxChars = 10_000;

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex HorizontalWhitespace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessNewlines();

    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var text = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        text = HorizontalWhitespace().Replace(text, " ");
        text = ExcessNewlines().Replace(text, "\n\n");
        text = text.Trim();

        if (text.Length > MaxChars)
            text = text[..MaxChars] + " […truncated]";
        return text;
    }
}
