using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Jobmatch.Cv;

// Turns an uploaded CV file (bytes + filename) into plain text. PDF is the
// dominant real-world CV format; .txt/.md pass through. .docx is unsupported —
// the GUI offers paste as the fallback.
public static class CvTextExtractor
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    public static string Extract(byte[] bytes, string fileName)
    {
        if (bytes.Length == 0)
            throw new InvalidRequestException("The CV file is empty.");
        if (bytes.LongLength > MaxFileBytes)
            throw new InvalidRequestException("The CV file exceeds the 10 MB limit.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".md" => Encoding.UTF8.GetString(bytes),
            ".pdf" => ExtractPdf(bytes),
            _ => throw new InvalidRequestException(
                $"Unsupported CV file type '{ext}' — upload a .pdf, .txt or .md file, or paste the text instead."),
        };
    }

    // ContentOrderTextExtractor follows the content stream order, which reads
    // multi-column CVs better than the raw glyph order of Page.Text.
    internal static string ExtractPdf(byte[] bytes)
    {
        try
        {
            using var document = PdfDocument.Open(bytes);
            var sb = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(ContentOrderTextExtractor.GetText(page));
            }
            return sb.ToString();
        }
        catch (Exception ex) when (ex is not InvalidRequestException)
        {
            throw new InvalidRequestException($"Could not read the PDF file ({ex.Message}).");
        }
    }
}
