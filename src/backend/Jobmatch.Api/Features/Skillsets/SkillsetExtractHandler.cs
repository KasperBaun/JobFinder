using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Cv;
using Jobmatch.Features.AiModel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Features.Skillsets;

public interface ISkillsetExtractHandler
{
    Task<IResult> Start(IFormFile? file, string? text, string? url);
    Task<IResult> Status();
}

// Starts a background CV → profile extraction and reports its status. The LLM
// readiness gate lives here so a POST fails fast with a clear 400 instead of
// surfacing the problem minutes later through the status poll.
public sealed class SkillsetExtractHandler(
    ILlmModelLocator model,
    CvExtractionManager extractions,
    ILogger<SkillsetExtractHandler> logger) : HandlerBase(logger), ISkillsetExtractHandler
{
    public Task<IResult> Start(IFormFile? file, string? text, string? url) => ExecuteAsync(
        "start cv extraction",
        async () =>
        {
            var source = await BuildSourceAsync(file, text, url).ConfigureAwait(false);
            model.EnsureReady();
            var snapshot = extractions.Start(source);
            Logger.LogInformation("CV extraction requested → state {State}", snapshot.State);
            return Results.Ok(ToResponse(snapshot));
        });

    public Task<IResult> Status() => ExecuteAsync(
        "cv extraction status",
        () => Task.FromResult<IResult>(Results.Ok(ToResponse(extractions.Snapshot()))));

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
