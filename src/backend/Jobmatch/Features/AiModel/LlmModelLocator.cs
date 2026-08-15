using Jobmatch.Infrastructure.Llm;
using Jobmatch.Search.Ranking;
using Jobmatch.Infrastructure.Paths;

namespace Jobmatch.Features.AiModel;

/// <summary>Where the model file is, and whether it is usable right now.</summary>
public interface ILlmModelLocator
{
    LlmConfig Config { get; }

    string AbsoluteModelPath { get; }

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

    public string AbsoluteModelPath => Config.AbsoluteModelPath(ctx.RootDir);

    public void EnsureReady()
    {
        var llm = Config;
        if (!llm.Enabled)
            throw new InvalidRequestException(
                "AI is disabled (llm.enabled in ranking.yml) — enable it to extract a profile from a CV.");

        // Only the in-process provider needs a local file; Ollama serves the model itself.
        if (!llm.Provider.Equals("llamasharp", StringComparison.OrdinalIgnoreCase))
            return;

        if (!File.Exists(llm.AbsoluteModelPath(ctx.RootDir)))
            throw new InvalidRequestException("The AI model has not been downloaded yet — download it first.");
    }
}
