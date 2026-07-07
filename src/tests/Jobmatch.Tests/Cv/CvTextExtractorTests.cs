using System.Text;
using Jobmatch.Cv;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Jobmatch.Tests.Cv;

public sealed class CvTextExtractorTests
{
    [Theory]
    [InlineData("cv.txt")]
    [InlineData("cv.md")]
    [InlineData("CV.TXT")]
    public void Extract_PlainText_PassesThrough(string fileName)
    {
        var text = "Jane Doe\nBackend Developer — C#, .NET";

        var result = CvTextExtractor.Extract(Encoding.UTF8.GetBytes(text), fileName);

        Assert.Equal(text, result);
    }

    [Fact]
    public void Extract_Pdf_ReturnsPageText()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Jane Doe Backend Developer", 12, new PdfPoint(30, 700), font);

        var result = CvTextExtractor.Extract(builder.Build(), "cv.pdf");

        Assert.Contains("Jane Doe Backend Developer", result);
    }

    [Fact]
    public void Extract_CorruptPdf_ThrowsInvalidRequest()
    {
        var ex = Assert.Throws<InvalidRequestException>(
            () => CvTextExtractor.Extract(Encoding.UTF8.GetBytes("%PDF-not really"), "cv.pdf"));
        Assert.Contains("PDF", ex.Message);
    }

    [Theory]
    [InlineData("cv.docx")]
    [InlineData("cv.png")]
    [InlineData("cv")]
    public void Extract_UnsupportedExtension_ThrowsInvalidRequest(string fileName)
    {
        Assert.Throws<InvalidRequestException>(
            () => CvTextExtractor.Extract([1, 2, 3], fileName));
    }

    [Fact]
    public void Extract_EmptyFile_ThrowsInvalidRequest()
    {
        Assert.Throws<InvalidRequestException>(() => CvTextExtractor.Extract([], "cv.txt"));
    }

    [Fact]
    public void Extract_OversizedFile_ThrowsInvalidRequest()
    {
        var bytes = new byte[CvTextExtractor.MaxFileBytes + 1];

        var ex = Assert.Throws<InvalidRequestException>(() => CvTextExtractor.Extract(bytes, "cv.txt"));
        Assert.Contains("10 MB", ex.Message);
    }
}
