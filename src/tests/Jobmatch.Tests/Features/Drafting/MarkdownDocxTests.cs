using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Jobmatch.Features.Drafting;

namespace Jobmatch.Tests.Features.Drafting;

public sealed class MarkdownDocxTests
{
    [Fact]
    public void Write_ProducesAPackageWordCanOpen()
    {
        using var stream = new MemoryStream();

        MarkdownDocx.Write("## Experience\n\n- Built **things**\n\nPlain line.", stream);

        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var main = doc.MainDocumentPart;
        Assert.NotNull(main);
        Assert.NotNull(main.NumberingDefinitionsPart);
        var body = main.Document?.Body;
        Assert.NotNull(body);
        Assert.Contains("Built", body.InnerText);
        Assert.Contains("Plain line.", body.InnerText);
    }

    // A pStyle pointing at a style the package does not define renders as body text everywhere except
    // Word, which quietly substitutes its own built-in. Defining them is what makes a heading a
    // heading in whatever the user opens it in.
    [Fact]
    public void Write_DefinesEveryStyleItReferences()
    {
        using var stream = new MemoryStream();
        MarkdownDocx.Write("# One\n\n## Two\n\n### Three\n\nBody.", stream);

        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var defined = doc.MainDocumentPart!.StyleDefinitionsPart?.Styles?.Elements<Style>()
            .Select(s => s.StyleId?.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotNull(defined);
        var referenced = doc.MainDocumentPart.Document!.Body!.Descendants<ParagraphStyleId>()
            .Select(p => p.Val?.Value);
        Assert.All(referenced, styleId => Assert.Contains(styleId, defined!));
        Assert.Contains("Normal", defined!);
    }

    [Fact]
    public void Write_SetsA4_NotWhateverTheReaderDefaultsTo()
    {
        using var stream = new MemoryStream();
        MarkdownDocx.Write("Body.", stream);

        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var pageSize = doc.MainDocumentPart!.Document!.Body!.Elements<SectionProperties>()
            .Single().Elements<PageSize>().Single();

        Assert.Equal(11906U, pageSize.Width!.Value);
        Assert.Equal(16838U, pageSize.Height!.Value);
    }

    // Word silently refuses to open a package that violates the schema, so "it wrote bytes" is not
    // evidence the export works. The validator is the check that it does.
    [Fact]
    public void Write_ProducesASchemaValidPackage()
    {
        using var stream = new MemoryStream();
        MarkdownDocx.Write("# Jane Doe\n\n## Experience\n\n- **Senior** dev\n- Another\n\nBody text.", stream);

        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var errors = new OpenXmlValidator().Validate(doc).ToList();

        Assert.Empty(errors.Select(e => $"{e.Path?.XPath}: {e.Description}"));
    }

    [Theory]
    [InlineData("# Title", "Heading1")]
    [InlineData("## Section", "Heading2")]
    [InlineData("### Sub", "Heading3")]
    public void Render_Headings_MapToWordStyles(string line, string expectedStyle)
    {
        var paragraph = Assert.Single(MarkdownDocx.Render(line));

        var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        Assert.Equal(expectedStyle, style);
    }

    // Word only has three heading levels wired up here; deeper hashes are body text, not a crash.
    [Fact]
    public void Render_TooManyHashes_StaysBodyText()
    {
        var paragraph = Assert.Single(MarkdownDocx.Render("#### Deep"));

        Assert.Null(paragraph.ParagraphProperties?.ParagraphStyleId);
        Assert.Equal("#### Deep", paragraph.InnerText);
    }

    [Fact]
    public void Render_HashWithoutSpace_IsNotAHeading()
    {
        var paragraph = Assert.Single(MarkdownDocx.Render("#hashtag"));

        Assert.Null(paragraph.ParagraphProperties?.ParagraphStyleId);
    }

    [Theory]
    [InlineData("- item")]
    [InlineData("* item")]
    public void Render_Bullets_GetNumberingProperties(string line)
    {
        var paragraph = Assert.Single(MarkdownDocx.Render(line));

        Assert.NotNull(paragraph.ParagraphProperties?.NumberingProperties);
        Assert.Equal("item", paragraph.InnerText);
    }

    [Fact]
    public void InlineRuns_BoldSegment_IsBoldAndSurroundingTextIsNot()
    {
        var runs = MarkdownDocx.InlineRuns("plain **loud** plain").ToList();

        Assert.Equal(3, runs.Count);
        Assert.Null(runs[0].RunProperties?.Bold);
        Assert.NotNull(runs[1].RunProperties?.Bold);
        Assert.Equal("loud", runs[1].InnerText);
        Assert.Null(runs[2].RunProperties?.Bold);
    }

    // An unmatched marker would otherwise swallow the rest of the line into a bold run.
    [Fact]
    public void InlineRuns_UnmatchedMarker_StaysLiteral()
    {
        var runs = MarkdownDocx.InlineRuns("a ** dangling").ToList();

        var run = Assert.Single(runs);
        Assert.Equal("a ** dangling", run.InnerText);
        Assert.Null(run.RunProperties?.Bold);
    }

    [Fact]
    public void Render_BlankLine_BecomesEmptyParagraph()
    {
        var paragraphs = MarkdownDocx.Render("a\n\nb").ToList();

        Assert.Equal(3, paragraphs.Count);
        Assert.Equal(string.Empty, paragraphs[1].InnerText);
    }

    [Fact]
    public void Render_CrLfInput_DoesNotProduceStrayParagraphs()
    {
        var paragraphs = MarkdownDocx.Render("a\r\nb").ToList();

        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("a", paragraphs[0].InnerText);
        Assert.Equal("b", paragraphs[1].InnerText);
    }

    [Fact]
    public void Render_LeadingWhitespaceIsPreservedInRuns()
    {
        var paragraph = Assert.Single(MarkdownDocx.Render("  indented body"));

        var text = paragraph.Descendants<Text>().Single();
        Assert.Equal("  indented body", text.Text);
    }
}
