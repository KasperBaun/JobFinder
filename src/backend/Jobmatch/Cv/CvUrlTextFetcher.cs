using AngleSharp.Html.Parser;

namespace Jobmatch.Cv;

// Fetches a user-provided CV URL (an explicit user action — not crawling) and
// converts the response to plain text: HTML is stripped to its body text, PDFs
// go through the PDF extractor, text/* passes through.
public static class CvUrlTextFetcher
{
    public static async Task<string> FetchAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidRequestException("The CV URL must be an http(s) address.");

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
            MaxResponseContentBufferSize = CvTextExtractor.MaxFileBytes,
        };

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(uri, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidRequestException($"Could not fetch the CV URL ({ex.Message}).");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new InvalidRequestException(
                    $"Could not fetch the CV URL ({(int)response.StatusCode} {response.ReasonPhrase}).");

            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

            if (mediaType == "application/pdf" || uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return CvTextExtractor.ExtractPdf(bytes);
            if (mediaType.Contains("html"))
                return HtmlToText(System.Text.Encoding.UTF8.GetString(bytes));
            if (mediaType.StartsWith("text/", StringComparison.Ordinal) || mediaType.Length == 0)
                return System.Text.Encoding.UTF8.GetString(bytes);

            throw new InvalidRequestException(
                $"The CV URL returned unsupported content ('{mediaType}') — expected an HTML page, a PDF, or plain text.");
        }
    }

    internal static string HtmlToText(string html)
    {
        var parser = new HtmlParser();
        using var document = parser.ParseDocument(html);
        foreach (var element in document.QuerySelectorAll("script, style, nav, header, footer"))
            element.Remove();
        return document.Body?.TextContent ?? string.Empty;
    }
}
