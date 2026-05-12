using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jobmatch.Llm;

// Minimal Ollama /api/chat wrapper. No streaming (we want the full response in one
// go), no embedding endpoint, no agentic tools — just send messages and get the
// model's text reply back. Throws on non-200; caller handles fallback. The power-
// user backend; the default shipped backend is LlamaSharpClient.
public sealed class OllamaClient(HttpClient http, string baseUrl, string model, double temperature = 0.0) : ILlmClient
{
    private readonly Uri _chatEndpoint = new(new Uri(baseUrl), "/api/chat");
    private readonly Uri _versionEndpoint = new(new Uri(baseUrl), "/api/version");

    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync(_versionEndpoint, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new ChatRequest(
            Model: model,
            Messages: [
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", userPrompt),
            ],
            Stream: false,
            Options: new ChatOptions(Temperature: temperature));

        using var response = await http.PostAsJsonAsync(_chatEndpoint, request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        var parsed = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("ollama returned null body");
        return parsed.Message?.Content ?? string.Empty;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record ChatRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        bool Stream,
        ChatOptions? Options);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatOptions(double Temperature);

    private sealed record ChatResponse(
        [property: JsonPropertyName("message")] ChatMessage? Message,
        [property: JsonPropertyName("done")] bool Done);
}
