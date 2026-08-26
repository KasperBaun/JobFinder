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

        AddNumbering(main);

        foreach (var paragraph in Render(markdown))
            body.AppendChild(paragraph);
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
