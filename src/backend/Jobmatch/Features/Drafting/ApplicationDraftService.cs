using Jobmatch.Features.Cv;
using Jobmatch.Features.History;
using Jobmatch.Features.Skillsets;
using Jobmatch.Infrastructure.IO;
using Jobmatch.Infrastructure.Llm;
using Jobmatch.Infrastructure.Paths;
using Jobmatch.Search.Ranking;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Features.Drafting;

/// <summary>
/// Resolves the listing's ad text and the user's CV, runs the model, and writes both documents.
/// Creates and disposes its own <see cref="ILlmClient"/> per call, like
/// <see cref="CvExtractionService"/> — a llamasharp model load costs seconds, which is noise next to
/// generating two documents.
/// </summary>
public sealed class ApplicationDraftService(
    UserContext ctx,
    IRunHistoryStore runs,
    ICvDocumentStore cv,
    ISkillsetService skillsets,
    TimeProvider clock,
    ILoggerFactory loggers) : IApplicationDraftService
{
    /// <summary>
    /// Two full documents need far more room than the judge's one-line verdict or extraction's single
    /// JSON object; below this the reply truncates mid-object and the parse fails.
    /// </summary>
    private const int MaxTokens = 2048;

    /// <summary>
    /// What the whole exchange needs to fit in: the CV and the ad on the way in, both documents on the
    /// way out. The shipped llamasharp default is 4096, sized for the judge's one-line verdict — under
    /// it a draft would silently truncate mid-document, so drafting raises it for its own client only.
    /// Search judging keeps the configured value, and a config that already asks for more is left alone.
    /// </summary>
    private const int MinContextSize = 8192;

    public async Task<DraftedDocuments> DraftAsync(string runId, string listingId, CancellationToken ct = default)
    {
        var jobAdText = ResolveJobAdText(runId, listingId);

        var cvText = cv.Find()
            ?? throw new InvalidRequestException(
                "No CV stored yet — add your CV before drafting an application. A resume can only be written from facts you have provided.");

        var llm = RankingConfigLoader.Load(ctx.RankingPath).Llm;
        if (!llm.Enabled)
            throw new InvalidRequestException("AI is disabled (llm.enabled in ranking.yml) — enable it to draft an application.");

        using var http = new HttpClient();
        var client = LlmClientFactory.Create(WithDraftingContext(llm), ctx.RootDir, http, loggers, MaxTokens)!;
        ApplicationDraft draft;
        try
        {
            var writer = new ApplicationDraftWriter(client, loggers.CreateLogger<ApplicationDraftWriter>());
            draft = await writer.WriteAsync(new DraftInputs(cvText, skillsets.Find(), jobAdText), ct).ConfigureAwait(false);
        }
        finally
        {
            (client as IDisposable)?.Dispose();
        }

        var (resumePath, coverLetterPath) = WriteDocuments(draft, listingId);
        return new DraftedDocuments(listingId, runId, draft, resumePath, coverLetterPath, clock.GetUtcNow());
    }

    // Ollama holds its own context settings, so only the in-process backend is adjusted here.
    internal static LlmConfig WithDraftingContext(LlmConfig llm) => llm.Provider switch
    {
        LlmProvider.LlamaSharp llama when llama.ContextSize < MinContextSize =>
            llm with { Provider = llama with { ContextSize = MinContextSize } },
        _ => llm,
    };

    private string ResolveJobAdText(string runId, string listingId)
    {
        var detail = runs.Find(runId)
            ?? throw new NotFoundException($"Run '{runId}' has no recorded results.");

        var match = detail.Shortlist.FirstOrDefault(m => string.Equals(m.Id, listingId, StringComparison.Ordinal))
            ?? throw new NotFoundException($"Listing '{listingId}' is not in run '{runId}'.");

        // Description is null on runs recorded before the ad text was persisted; title and company
        // alone are not enough to tailor against, so this is a hard stop rather than a thin draft.
        if (string.IsNullOrWhiteSpace(match.Description))
            throw new InvalidRequestException(
                "That listing was saved without its ad text, so there is nothing to tailor against. Run a fresh search and draft from that run.");

        return match.Description;
    }

    private (string ResumePath, string CoverLetterPath) WriteDocuments(ApplicationDraft draft, string listingId)
    {
        Directory.CreateDirectory(ctx.DocumentsDir);

        var resumePath = Path.Combine(ctx.DocumentsDir, DraftFileNames.Resume(draft, listingId));
        var coverLetterPath = Path.Combine(ctx.DocumentsDir, DraftFileNames.CoverLetter(draft, listingId));

        AtomicFile.WriteStream(resumePath, s => MarkdownDocx.Write(draft.ResumeMarkdown, s));
        AtomicFile.WriteStream(coverLetterPath, s => MarkdownDocx.Write(draft.CoverLetterMarkdown, s));

        return (resumePath, coverLetterPath);
    }
}
