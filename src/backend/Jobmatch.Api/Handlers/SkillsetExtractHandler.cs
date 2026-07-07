using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Configuration;
using Jobmatch.Cv;
using Jobmatch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface ISkillsetExtractHandler
{
    Task<IResult> Start(IFormFile? file, string? text, string? url);
    Task<IResult> Status();
}

// Starts a background CV → profile extraction and reports its status. The LLM
// readiness gate lives here so a POST fails fast with a clear 400 instead of
// surfacing the problem minutes later through the status poll.
public sealed class SkillsetExtractHandler(
    UserContext ctx,
    CvExtractionManager extractions,
    ILogger<SkillsetExtractHandler> logger) : HandlerBase(logger), ISkillsetExtractHandler
{
    public Task<IResult> Start(IFormFile? file, string? text, string? url) => ExecuteAsync(
        "start cv extraction",
        async () =>
        {
            var source = await BuildSourceAsync(file, text, url).ConfigureAwait(false);
            EnsureLlmReady();
            var snapshot = extractions.Start(source);
            Logger.LogInformation("CV extraction requested → state {State}", snapshot.State);
            return Results.Ok(ToResponse(snapshot));
        });

    public Task<IResult> Status() => ExecuteAsync(
        "cv extraction status",
        () => Task.FromResult<IResult>(Results.Ok(ToResponse(extractions.Snapshot()))));

    private void EnsureLlmReady()
    {
        var llm = RankingConfigLoader.Load(ctx.RankingPath).Llm;
        if (!llm.Enabled)
            throw new InvalidRequestException("AI is disabled (llm.enabled in ranking.yml) — enable it to extract a profile from a CV.");
        if (llm.Provider.Equals("llamasharp", StringComparison.OrdinalIgnoreCase))
        {
            var path = Path.IsPathRooted(llm.ModelPath) ? llm.ModelPath : Path.Combine(ctx.RootDir, llm.ModelPath);
            if (!File.Exists(path))
                throw new InvalidRequestException("The AI model has not been downloaded yet — download it first.");
        }
    }

    private static async Task<CvSource> BuildSourceAsync(IFormFile? file, string? text, string? url)
    {
        var provided = (file is not null ? 1 : 0)
            + (NullIfBlank(text) is not null ? 1 : 0)
            + (NullIfBlank(url) is not null ? 1 : 0);
        if (provided != 1)
            throw new InvalidRequestException("Provide exactly one of: pasted text, a CV file, or a CV URL.");

        if (file is null)
            return new CvSource(NullIfBlank(text), null, null, NullIfBlank(url));

        if (file.Length > CvTextExtractor.MaxFileBytes)
            throw new InvalidRequestException("The CV file exceeds the 10 MB limit.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer).ConfigureAwait(false);
        return new CvSource(null, buffer.ToArray(), file.FileName, null);
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static CvExtractionStatusResponse ToResponse(CvExtractionSnapshot s) =>
        new(s.State, s.StartedAt, s.Error, s.Profile);
}
