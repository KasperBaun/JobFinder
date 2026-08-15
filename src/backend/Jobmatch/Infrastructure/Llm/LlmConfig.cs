namespace Jobmatch.Infrastructure.Llm;

/// <summary>
/// How to reach a local language model. Parsed from the <c>llm:</c> block of ranking.yml, but not a
/// ranking concept — the judge, the CV extractor and the model-download endpoint all take one.
/// </summary>
public sealed record LlmConfig(
    bool Enabled,
    string Provider,        // "llamasharp" | "ollama"
    string Model,           // ollama: model tag, e.g. "gemma3:4b". llamasharp: ignored (model file is ModelPath)
    string ModelPath,       // llamasharp: absolute or data-relative path to GGUF file. ollama: ignored
    string ModelDownloadUrl, // llamasharp: where to fetch the GGUF file from on first run
    string BaseUrl,         // ollama only — the HTTP endpoint to hit. llamasharp: ignored
    int TopN,               // judge only the top-N from keyword ranker (0 = all)
    double Weight,          // 0.0 = keyword-only, 1.0 = LLM-only, 0.5 = blend equally
    double Temperature,     // 0.0 = deterministic
    int ContextSize,        // llamasharp only — model context window in tokens
    int GpuLayerCount)      // llamasharp only — layers offloaded to GPU (0 = CPU-only)
{
    // A relative ModelPath is relative to the user's own data directory, so the default
    // `models/gemma-3-4b-it-q4_k_m.gguf` lands at data/<email>/models/gemma-3-4b-it-q4_k_m.gguf.
    public string AbsoluteModelPath(string userDataDir) =>
        Path.IsPathRooted(ModelPath) ? ModelPath : Path.Combine(userDataDir, ModelPath);

    public static LlmConfig Disabled { get; } = new(
        Enabled: false,
        Provider: "llamasharp",
        Model: "gemma3:4b",
        ModelPath: "models/gemma-3-4b-it-q4_k_m.gguf",
        ModelDownloadUrl: "https://huggingface.co/mradermacher/gemma-3-4b-it-GGUF/resolve/main/gemma-3-4b-it.Q4_K_M.gguf",
        BaseUrl: "http://localhost:11434",
        TopN: 50,
        Weight: 0.5,
        Temperature: 0.0,
        ContextSize: 4096,
        GpuLayerCount: 0);
}
