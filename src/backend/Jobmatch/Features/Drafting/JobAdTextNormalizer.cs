using System.Text;
using System.Text.RegularExpressions;

namespace Jobmatch.Features.Drafting;

// Strips the page furniture a scraped ad carries — CSS blocks, inline scripts, comments — and
// reports how much continuous prose survived. A description is persisted as one collapsed line, so
// this works on structure rather than on lines: braced spans go first, then whatever statements are
// left around them.
public static partial class JobAdTextNormalizer
{
    /// <summary>
    /// Below this much continuous prose there is no ad to tailor against — a company name and some
    /// navigation chrome, or a syndicated teaser that stops after the first sentence. Across the
    /// shipped portals the thinnest real ad clears 2,600 characters and the richest junk reaches 183,
    /// so the threshold sits in a wide empty valley rather than on a boundary.
    /// </summary>
    public const int MinProseChars = 400;

    /// <summary>A run of this many words is a sentence; anything shorter is a label or a CSS property.</summary>
    private const int WordsPerProseRun = 6;

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"[\w$]+(?:\.[\w$]+)+\s*\([^)]{0,200}\)")]
    private static partial Regex MethodCall();

    [GeneratedRegex(@"\b(?:function|var|let|const)\s+[\w$]+\s*(?:\([^)]{0,120}\))?")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"[;={}]+")]
    private static partial Regex Punctuation();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // Everything a sentence cannot contain. Digits separate too: "25px 20px" falls apart into labels
    // while "over 50 years, we have worked closely with" keeps a clause long enough to count.
    [GeneratedRegex(@"[^\w \.,'’\-!?]|[\d_]")]
    private static partial Regex NotSentence();

    /// <summary>
    /// The ad with its markup furniture removed, or <see cref="string.Empty"/> when what is left is
    /// not an ad — the caller refuses rather than tailoring an application to navigation chrome.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var text = BlockComment().Replace(raw, " ");
        text = HtmlComment().Replace(text, " ");
        text = StripBracedSpans(text);
        text = MethodCall().Replace(text, " ");
        text = Declaration().Replace(text, " ");
        text = Punctuation().Replace(text, " ");
        text = Whitespace().Replace(text, " ").Trim();

        return ProseLength(text) >= MinProseChars ? text : string.Empty;
    }

    /// <summary>
    /// Drops every <c>{…}</c> span that actually closes, outermost first — one pass removes CSS rule
    /// bodies, script blocks and embedded JSON alike. An unmatched brace is left where it is: a stray
    /// <c>{</c> in real ad text must not swallow the rest of the ad.
    /// </summary>
    internal static string StripBracedSpans(string text)
    {
        var open = new Stack<int>();
        var spans = new List<(int Start, int End)>();

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                open.Push(i);
            }
            else if (text[i] == '}' && open.Count > 0)
            {
                var start = open.Pop();
                if (open.Count == 0) spans.Add((start, i));
            }
        }

        if (spans.Count == 0) return text;

        var sb = new StringBuilder(text.Length);
        var cursor = 0;
        foreach (var (start, end) in spans)
        {
            sb.Append(text, cursor, start - cursor).Append(' ');
            cursor = end + 1;
        }

        return sb.Append(text, cursor, text.Length - cursor).ToString();
    }

    /// <summary>
    /// How many characters sit inside stretches long enough to be sentences. Counting these rather
    /// than counting words is what separates an ad from leftover CSS, whose property names are
    /// perfectly good words but never line up into a clause.
    /// </summary>
    internal static int ProseLength(string text)
    {
        var total = 0;
        foreach (var fragment in NotSentence().Replace(text, "\n").Split('\n'))
        {
            var words = 0;
            foreach (var token in fragment.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length >= 2 && token.Any(char.IsLetter)) words++;
            }

            if (words >= WordsPerProseRun) total += fragment.Trim().Length;
        }

        return total;
    }
}
