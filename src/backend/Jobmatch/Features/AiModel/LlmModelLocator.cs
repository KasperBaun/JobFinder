using Jobmatch.Infrastructure.Llm;
using Jobmatch.Infrastructure.Paths;
using Jobmatch.Search.Ranking;

namespace Jobmatch.Features.AiModel;

/// <summary>A model file this install must have on disk: where it goes, and where it comes from.</summary>
public sealed record RequiredModel(string AbsolutePath, Uri DownloadUrl);

/// <summary>Where the model file is, and whether it is usable right now.</summary>
public interface ILlmModelLocator
{
    LlmConfig Config { get; }

    /// <summary>Null when the provider holds the model itself, so there is nothing to fetch.</summary>
    RequiredModel? RequiredModel { get; }

    /// <summary>
    /// Throws <see cref="InvalidRequestException"/> with a message the GUI can show if AI cannot run:
    /// switched off in config, or an in-process model that has not been downloaded.
    /// </summary>
    void EnsureReady();
}

/// <summary>
/// Resolves the LLM config and model path for callers that need to know whether AI is available
/// before starting work. Two API handlers each reimplemented this — reading ranking.yml, then
/// joining a possibly-relative model path onto the data directory — and only one of them checked
/// that the file actually existed.
/// </summary>
public sealed class LlmModelLocator(UserContext ctx) : ILlmModelLocator
{
    // Every read re-parses ranking.yml, so callers that need it twice hold onto one copy.
    public LlmConfig Config => RankingConfigLoader.Load(ctx.RankingPath).Llm;

    public RequiredModel? RequiredModel => Required(Config);

    public void EnsureReady()
    {
        var llm = Config;
        if (!llm.Enabled)
            throw new InvalidRequestException(
                "AI is disabled (llm.enabled in ranking.yml) — enable it to extract a profile from a CV.");

        if (Required(llm) is not { } model) return;

        if (!File.Exists(model.AbsolutePath))
            throw new InvalidRequestException("The AI model has not been downloaded yet — download it first.");
    }

    private RequiredModel? Required(LlmConfig llm) => llm.Provider switch
    {
        LlmProvider.LlamaSharp llama =>
            new RequiredModel(llama.Model.AbsolutePath(ctx.RootDir), llama.Model.DownloadUrl),
        _ => null,
    };
}
