using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Jobmatch.Features.Drafting;

/// <summary>
/// Renders the Markdown subset the drafting prompt asks for — <c>#</c>/<c>##</c>/<c>###</c> headings,
/// <c>-</c> and <c>*</c> bullets, <c>**bold**</c>, plain paragraphs — into a .docx. Anything else
/// falls through as body text, so an unexpected construct degrades to a readable line rather than
/// failing the export.
/// </summary>
public static class MarkdownDocx
{
    public static void Write(string markdown, Stream destination)
    {
        using var doc = WordprocessingDocument.Create(destination, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document();
        var body = main.Document.AppendChild(new Body());

        AddStyles(main);
        AddNumbering(main);

        foreach (var paragraph in Render(markdown))
            body.AppendChild(paragraph);

        body.AppendChild(PageSetup());
    }

    internal static IEnumerable<Paragraph> Render(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0)
            {
                yield return new Paragraph(new Run());
                continue;
            }

            if (IsThematicBreak(line)) continue;

            if (TryHeading(line, out var level, out var headingText))
            {
                yield return Styled(headingText, $"Heading{level}");
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.Length > 2 && (trimmed[0] is '-' or '*') && trimmed[1] == ' ')
            {
                yield return Bulleted(trimmed[2..].Trim());
                continue;
            }

            if (TryBoldHeading(trimmed, out var boldLevel, out var boldText))
            {
                yield return Styled(boldText, $"Heading{boldLevel}");
                continue;
            }

            yield return Body(line);
        }
    }

    private static bool TryHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;
        var hashes = 0;
        while (hashes < line.Length && line[hashes] == '#') hashes++;
        if (hashes is < 1 or > 3) return false;
        if (hashes >= line.Length || line[hashes] != ' ') return false;

        level = hashes;
        text = line[(hashes + 1)..].Trim();
        return true;
    }

    /// <summary>Longer than this and a wholly-bold line is a sentence someone emphasised, not a heading.</summary>
    internal const int MaxBoldHeadingLength = 80;

    /// <summary>
    /// A line that is nothing but one short bold run is the heading it was meant to be. Asked for
    /// <c>## </c> headings a small model still reaches for <c>**SUMMARY**</c> in half the drafts, and
    /// bold-as-heading renders as body text — so the alternative to reading it this way is a resume
    /// with a section that is not a section. A section label is a bare word or two; an entry carries a
    /// comma, a bracket or a year, and sits one level below it.
    /// </summary>
    internal static bool TryBoldHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;

        var trimmed = line.Trim();
        if (trimmed.Length <= 4
            || !trimmed.StartsWith("**", StringComparison.Ordinal)
            || !trimmed.EndsWith("**", StringComparison.Ordinal)) return false;

        var inner = trimmed[2..^2].Trim();
        // Two bold runs on one line is a sentence with emphasis in it, not a heading.
        if (inner.Length == 0 || inner.Length > MaxBoldHeadingLength
            || inner.Contains("**", StringComparison.Ordinal)) return false;

        text = inner;
        level = inner.Any(c => c is ',' or '(' or ')') || inner.Any(char.IsDigit) ? 3 : 2;
        return true;
    }

    /// <summary>A rule carries no content in a resume, and Word has no glyph for it — drop it.</summary>
    private static bool IsThematicBreak(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3
            && (trimmed.All(c => c == '-') || trimmed.All(c => c == '*') || trimmed.All(c => c == '_'));
    }

    private static Paragraph Styled(string text, string styleId)
    {
        var p = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        foreach (var run in InlineRuns(text)) p.AppendChild(run);
        return p;
    }

    private static Paragraph Body(string text)
    {
        var p = new Paragraph();
        foreach (var run in InlineRuns(text)) p.AppendChild(run);
        return p;
    }

    private static Paragraph Bulleted(string text)
    {
        var props = new ParagraphProperties(
            new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = BulletNumberingId }),
            new Indentation { Left = "720", Hanging = "360" });

        var p = new Paragraph(props);
        foreach (var run in InlineRuns(text)) p.AppendChild(run);
        return p;
    }

    /// <summary>Splits on <c>**bold**</c>; an unmatched <c>**</c> stays literal rather than swallowing the rest.</summary>
    internal static IEnumerable<Run> InlineRuns(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            var open = text.IndexOf("**", index, StringComparison.Ordinal);
            if (open < 0)
            {
                yield return TextRun(text[index..], bold: false);
                yield break;
            }

            var close = text.IndexOf("**", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                yield return TextRun(text[index..], bold: false);
                yield break;
            }

            if (open > index) yield return TextRun(text[index..open], bold: false);
            yield return TextRun(text[(open + 2)..close], bold: true);
            index = close + 2;
        }
    }

    private static Run TextRun(string text, bool bold)
    {
        var run = new Run();
        if (bold) run.AppendChild(new RunProperties(new Bold()));
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private const int BulletNumberingId = 1;

    // A4, with 2cm margins, in twentieths of a point. Without a section, a reader picks its own
    // default — which is Letter in a US build, and the resume is going to a Danish employer.
    private static SectionProperties PageSetup() => new(
        new PageSize { Width = 11906U, Height = 16838U },
        new PageMargin
        {
            Top = 1134,
            Bottom = 1134,
            Left = 1134U,
            Right = 1134U,
            Header = 709U,
            Footer = 709U,
            Gutter = 0U,
        });

    // A paragraph that points at a style the document does not define renders as body text — Word
    // quietly substitutes its built-in heading, other readers do not, so the same file looks
    // structured in one and flat in the next. Defining them is what makes the headings headings.
    private static void AddStyles(MainDocumentPart main)
    {
        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(
                    new RunPropertiesBaseStyle(
                        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
                        new FontSize { Val = "22" }))),
            NormalStyle(),
            HeadingStyle(1, "32"),
            HeadingStyle(2, "26"),
            HeadingStyle(3, "24"));
    }

    private static Style NormalStyle() => new(
        new StyleName { Val = "Normal" },
        new PrimaryStyle())
    {
        Type = StyleValues.Paragraph,
        StyleId = "Normal",
        Default = true,
    };

    private static Style HeadingStyle(int level, string halfPoints) => new(
        new StyleName { Val = $"heading {level}" },
        new BasedOn { Val = "Normal" },
        new NextParagraphStyle { Val = "Normal" },
        new PrimaryStyle(),
        new StyleParagraphProperties(
            new KeepNext(),
            new SpacingBetweenLines { Before = "240", After = "120" }),
        new StyleRunProperties(
            new Bold(),
            new FontSize { Val = halfPoints }))
    {
        Type = StyleValues.Paragraph,
        StyleId = $"Heading{level}",
    };

    // Word renders a bulleted paragraph only when the numbering part defines the list it points at.
    private static void AddNumbering(MainDocumentPart main)
    {
        var part = main.AddNewPart<NumberingDefinitionsPart>();
        part.Numbering = new Numbering(
            new AbstractNum(
                new Level(
                    new NumberingFormat { Val = NumberFormatValues.Bullet },
                    new LevelText { Val = "•" },
                    new ParagraphProperties(new Indentation { Left = "720", Hanging = "360" }),
                    new RunProperties(new RunFonts { Ascii = "Symbol", HighAnsi = "Symbol" }))
                { LevelIndex = 0 })
            { AbstractNumberId = 1 },
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = BulletNumberingId });
    }
}
