using Jobmatch.Models;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Llm;

// Builds an ILlmClient from LlmConfig. Returns null when disabled — callers
// should treat null as "skip LLM judging, fall back to keyword scores". The
// model path for the embedded llamasharp backend is resolved against the user's
// data directory so users can manage their own model files there.
public static class LlmClientFactory
{
    // maxTokens applies to the llamasharp backend only (Ollama generates to the model's own
    // limit). The 128 default suits the judge's one-line verdict; callers expecting a larger
    // JSON reply (CV extraction) must raise it or the output silently truncates mid-object.
    public static ILlmClient? Create(LlmConfig config, string userDataDir, HttpClient http, ILoggerFactory loggers, int maxTokens = 128)
    {
        if (!config.Enabled) return null;

        return config.Provider.ToLowerInvariant() switch
        {
            "ollama" => new OllamaClient(http, config.BaseUrl, config.Model, config.Temperature),
            "llamasharp" => new LlamaSharpClient(
                ResolveModelPath(config.ModelPath, userDataDir),
                loggers.CreateLogger<LlamaSharpClient>(),
                contextSize: config.ContextSize,
                gpuLayerCount: config.GpuLayerCount,
                maxTokens: maxTokens,
                temperature: (float)config.Temperature),
            _ => throw new ConfigException(
                $"llm.provider must be one of [llamasharp, ollama]; got '{config.Provider}'"),
        };
    }

    private static string ResolveModelPath(string configured, string userDataDir)
    {
        // Absolute path → use as-is. Relative → resolve against the user's data dir
        // (so e.g. `models/gemma-3-4b-it-q4_k_m.gguf` lands at
        // data/<email>/models/gemma-3-4b-it-q4_k_m.gguf).
        if (Path.IsPathRooted(configured)) return configured;
        return Path.Combine(userDataDir, configured);
    }
}
