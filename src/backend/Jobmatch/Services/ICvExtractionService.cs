using Jobmatch.Configuration;
using Jobmatch.Cv;
using Jobmatch.Llm;
using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Services;

// Exactly one of Text / FileBytes / Url must be set.
public sealed record CvSource(string? Text, byte[]? FileBytes, string? FileName, string? Url);

public interface ICvExtractionService
{
    Task<ExtractedProfile> ExtractAsync(CvSource source, CancellationToken ct = default);
}

// Resolves the CV source to text, then runs the LLM extraction. Creates and
// disposes its own ILlmClient per call (same lifecycle as a search run) — a
// llamasharp model load costs a few seconds, acceptable for a one-off action.
public sealed class CvExtractionService(UserContext ctx, ILoggerFactory loggers) : ICvExtractionService
{
    public async Task<ExtractedProfile> ExtractAsync(CvSource source, CancellationToken ct = default)
    {
        var text = await ResolveTextAsync(source, ct).ConfigureAwait(false);
        var normalized = CvTextNormalizer.Normalize(text);
        if (normalized.Length == 0)
            throw new InvalidRequestException("No readable text found in the CV.");

        var llm = RankingConfigLoader.Load(ctx.RankingPath).Llm;
        if (!llm.Enabled)
            throw new InvalidRequestException("AI is disabled (llm.enabled in ranking.yml) — enable it to extract a profile from a CV.");

        using var http = new HttpClient();
        var client = LlmClientFactory.Create(llm, ctx.RootDir, http, loggers, maxTokens: 1024)!;
        try
        {
            var extractor = new CvProfileExtractor(client, loggers.CreateLogger<CvProfileExtractor>());
            return await extractor.ExtractAsync(normalized, ct).ConfigureAwait(false);
        }
        finally
        {
            (client as IDisposable)?.Dispose();
        }
    }

    private static async Task<string> ResolveTextAsync(CvSource source, CancellationToken ct)
    {
        var provided = (source.Text is not null ? 1 : 0)
            + (source.FileBytes is not null ? 1 : 0)
            + (source.Url is not null ? 1 : 0);
        if (provided != 1)
            throw new InvalidRequestException("Provide exactly one of: pasted text, a CV file, or a CV URL.");

        if (source.Text is not null) return source.Text;
        if (source.FileBytes is not null) return CvTextExtractor.Extract(source.FileBytes, source.FileName ?? string.Empty);
        return await CvUrlTextFetcher.FetchAsync(source.Url!, ct).ConfigureAwait(false);
    }
}
