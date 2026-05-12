namespace Jobmatch.Llm;

// Minimal LLM transport. Two implementations: LlamaSharpClient (in-process,
// llama.cpp via NuGet, no external deps — the default for shipped builds) and
// OllamaClient (HTTP to a user-managed Ollama install — the power-user opt-in).
// Both are sequential one-shot completion APIs; we don't need streaming because
// the LlmJudge wants the full response text in one go.
public interface ILlmClient
{
    Task<bool> IsReachableAsync(CancellationToken ct = default);
    Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
