namespace Jobmatch.Infrastructure.Llm;

public interface ILlmClient
{
    Task<bool> IsReachableAsync(CancellationToken ct = default);
    Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
