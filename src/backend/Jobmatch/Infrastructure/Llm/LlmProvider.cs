namespace Jobmatch.Infrastructure.Llm;

/// <summary>
/// Which backend serves the model, carrying only the settings that backend actually has. The
/// hierarchy is closed — the private constructor means these two are the only cases — so a
/// provider jobfinder does not support cannot be constructed, only rejected while parsing
/// ranking.yml. Downstream code pattern-matches instead of re-validating a string.
/// </summary>
public abstract record LlmProvider
{
    private LlmProvider() { }

    /// <summary>The spelling in ranking.yml, and what <c>/api/llm/status</c> reports.</summary>
    public abstract string Name { get; }

    /// <summary>llama.cpp in-process over a local GGUF. The default: no network at rank time.</summary>
    public sealed record LlamaSharp(LlmModelFile Model, int ContextSize, int GpuLayerCount) : LlmProvider
    {
        public const string ConfigName = "llamasharp";

        public static LlamaSharp Defaults { get; } =
            new(LlmModelFile.ShippedDefault, ContextSize: 4096, GpuLayerCount: 0);

        public override string Name => ConfigName;
    }

    /// <summary>A local Ollama server, which holds the model itself. Opt-in; nothing to download.</summary>
    public sealed record Ollama(Uri BaseUrl, string ModelTag) : LlmProvider
    {
        public const string ConfigName = "ollama";

        public static Ollama Defaults { get; } =
            new(new Uri("http://localhost:11434"), ModelTag: "gemma3:4b");

        public override string Name => ConfigName;
    }

    public static string SupportedNames => $"{LlamaSharp.ConfigName}, {Ollama.ConfigName}";
}
