namespace Jobmatch.Models;

public sealed record RankingConfig(
    RankingWeights Weights,
    double DisqualifierPenalty,
    int TopN,
    double FreshnessHalfLifeDays,
    double MinScoreToInclude,
    int? MaxAgeDays = null,
    bool RequirePrimaryStackHit = false,
    double SeniorityAdjacencyCredit = 1.0,
    double NonEngineeringTitleMultiplier = 0.2,
    double PreferredCompanyBoost = 1.25)
{
    public LocationTierWeights LocationTierWeights { get; init; } = LocationTierWeights.Default;
    public LlmConfig Llm { get; init; } = LlmConfig.Disabled;
}

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

public sealed record RankingWeights(
    double PrimaryStack,
    double SecondaryStack,
    double Seniority,
    double LocationRemote,
    double Domain,
    double Freshness)
{
    public double Sum() => PrimaryStack + SecondaryStack + Seniority + LocationRemote + Domain + Freshness;
}

public sealed record LocationTierWeights(
    double City,
    double Metro,
    double Country,
    double Region,
    double Else)
{
    public static LocationTierWeights Default { get; } = new(City: 1.0, Metro: 0.85, Country: 0.6, Region: 0.3, Else: 0.1);
}
