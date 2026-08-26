using Jobmatch.Features.Drafting;
using Jobmatch.Infrastructure.Llm;

namespace Jobmatch.Tests.Features.Drafting;

/// <summary>
/// The shipped llamasharp context is sized for the judge's one-line verdict. A draft sends a CV and an
/// ad and expects two documents back, which does not fit — and llama.cpp does not fail when it
/// overruns, it truncates, so the symptom would be a half-written resume rather than an error.
/// </summary>
public sealed class ApplicationDraftServiceContextTests
{
    [Fact]
    public void ShippedLlamaSharpDefault_IsRaisedForDrafting()
    {
        var config = Enabled(LlmProvider.LlamaSharp.Defaults);

        var adjusted = ApplicationDraftService.WithDraftingContext(config);

        var llama = Assert.IsType<LlmProvider.LlamaSharp>(adjusted.Provider);
        Assert.True(llama.ContextSize > LlmProvider.LlamaSharp.Defaults.ContextSize);
    }

    [Fact]
    public void RaisedContext_StillFitsCvAdPromptAndReply()
    {
        var adjusted = ApplicationDraftService.WithDraftingContext(Enabled(LlmProvider.LlamaSharp.Defaults));

        var llama = Assert.IsType<LlmProvider.LlamaSharp>(adjusted.Provider);
        // ~4 characters per token is the usual rule of thumb for this model family.
        var promptTokens = (ApplicationDraftWriter.MaxCvChars + ApplicationDraftWriter.MaxAdChars) / 4;
        Assert.True(
            promptTokens + 2048 < llama.ContextSize,
            $"prompt ≈{promptTokens} tokens plus the reply budget must fit {llama.ContextSize}");
    }

    [Fact]
    public void AlreadyGenerousContext_IsLeftAlone()
    {
        var configured = LlmProvider.LlamaSharp.Defaults with { ContextSize = 32_768 };

        var adjusted = ApplicationDraftService.WithDraftingContext(Enabled(configured));

        Assert.Equal(32_768, Assert.IsType<LlmProvider.LlamaSharp>(adjusted.Provider).ContextSize);
    }

    [Fact]
    public void OtherLlamaSharpSettings_SurviveTheAdjustment()
    {
        var configured = LlmProvider.LlamaSharp.Defaults with { GpuLayerCount = 42 };

        var adjusted = ApplicationDraftService.WithDraftingContext(Enabled(configured));

        var llama = Assert.IsType<LlmProvider.LlamaSharp>(adjusted.Provider);
        Assert.Equal(42, llama.GpuLayerCount);
        Assert.Equal(configured.Model, llama.Model);
    }

    // Ollama holds its own context settings, so there is nothing here to override.
    [Fact]
    public void Ollama_IsUntouched()
    {
        var config = Enabled(LlmProvider.Ollama.Defaults);

        var adjusted = ApplicationDraftService.WithDraftingContext(config);

        Assert.Same(config.Provider, adjusted.Provider);
    }

    private static LlmConfig Enabled(LlmProvider provider) =>
        LlmConfig.Disabled with { Enabled = true, Provider = provider };
}
