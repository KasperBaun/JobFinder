using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Infrastructure.Llm;

// Builds an ILlmClient from LlmConfig. Returns null when disabled — callers
// should treat null as "skip LLM judging, fall back to keyword scores".
public static class LlmClientFactory
{
    // maxTokens applies to the llamasharp backend only (Ollama generates to the model's own
    // limit). The 128 default suits the judge's one-line verdict; callers expecting a larger
    // JSON reply (CV extraction) must raise it or the output silently truncates mid-object.
    public static ILlmClient? Create(LlmConfig config, string userDataDir, HttpClient http, ILoggerFactory loggers, int maxTokens = 128)
    {
        if (!config.Enabled) return null;

        return config.Provider switch
        {
            LlmProvider.Ollama ollama =>
                new OllamaClient(http, ollama.BaseUrl.ToString(), ollama.ModelTag, config.Temperature),

            LlmProvider.LlamaSharp llama => new LlamaSharpClient(
                llama.Model.AbsolutePath(userDataDir),
                loggers.CreateLogger<LlamaSharpClient>(),
                contextSize: llama.ContextSize,
                gpuLayerCount: llama.GpuLayerCount,
                maxTokens: maxTokens,
                temperature: (float)config.Temperature),

            // LlmProvider's constructor is private, so these are the only two cases that exist.
            _ => throw new UnreachableException(),
        };
    }
}
