using System.Text;
using LLama;
using LLama.Batched;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Llm;

// In-process LLM via llama.cpp (LLamaSharp NuGet). The default shipped backend.
// Loads a GGUF model file from disk; if the file isn't present, IsReachableAsync
// returns false and the LlmJudge falls back to keyword-only ranking. Recommended
// model is Gemma 3 4B Q4_K_M (~2.5 GB on disk); the path comes from config.
//
// Loaded once and reused for the lifetime of the process — model load is the
// expensive step (~5-15s on cold start).
//
// System-prompt KV caching: the judge sends the same (large) system prompt on every
// listing and only varies the user turn. Re-encoding that shared prefix per call is
// the dominant cost, so we encode it once into a "warm" conversation via the
// BatchedExecutor, then Fork() it per listing (copy-on-write — the prefix KV is
// shared, not recomputed). Each fork adds only the user turn + generation and is
// disposed afterwards, leaving the primed prefix intact for the next listing.
public sealed class LlamaSharpClient : ILlmClient, IDisposable
{
    // Reproduces exactly what ChatSession's DefaultHistoryTransform fed the model
    // before: "System: {sys}<NL>User: {user}<NL>" with no assistant cue. The split
    // point is the inter-turn newline so the base conversation holds only the
    // run-invariant system turn. NL is Environment.NewLine, matching the AppendLine
    // used both here and inside the transform.
    private static readonly string[] AntiPrompts = ["User:", "\nUser", "</user>"];

    private readonly string _modelPath;
    private readonly int _contextSize;
    private readonly int _gpuLayerCount;
    private readonly int _maxTokens;
    private readonly float _temperature;
    private readonly ILogger<LlamaSharpClient> _logger;
    private readonly object _initLock = new();
    private LLamaWeights? _weights;
    private BatchedExecutor? _executor;
    private Conversation? _baseConv;
    private string? _primedSystem;

    public LlamaSharpClient(
        string modelPath,
        ILogger<LlamaSharpClient> logger,
        int contextSize = 4096,
        int gpuLayerCount = 0,
        int maxTokens = 128,
        float temperature = 0.0f)
    {
        _modelPath = modelPath;
        _logger = logger;
        _contextSize = contextSize;
        _gpuLayerCount = gpuLayerCount;
        _maxTokens = maxTokens;
        _temperature = temperature;
    }

    public Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_modelPath))
        {
            _logger.LogWarning("LlamaSharp model file not found at {Path} — judging will fall back to keyword-only.", _modelPath);
            return Task.FromResult(false);
        }
        try
        {
            EnsureLoaded();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load LlamaSharp model from {Path}", _modelPath);
            return Task.FromResult(false);
        }
    }

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        EnsureLoaded();
        await EnsurePrimedAsync(systemPrompt, ct);

        // Fork the warm system prefix (cheap, copy-on-write) and append only this
        // listing's user turn. Disposing the fork frees its KV delta; the base
        // conversation keeps the primed prefix for the next call.
        using var fork = _baseConv!.Fork();
        var suffix = _executor!.Context.Tokenize(UserTurn(userPrompt), addBos: false, special: true);
        fork.Prompt(suffix);
        return await GenerateAsync(fork, ct);
    }

    private async Task EnsurePrimedAsync(string systemPrompt, CancellationToken ct)
    {
        if (_primedSystem == systemPrompt && _baseConv is not null) return;

        _baseConv?.Dispose();
        _baseConv = _executor!.Create();
        var prefix = _executor.Context.Tokenize(SystemTurn(systemPrompt), addBos: true, special: true);
        _baseConv.Prompt(prefix);
        if (!await InferToReadyAsync(_baseConv, ct))
            throw new InvalidOperationException("LlamaSharp failed to prefill the system prompt");
        _primedSystem = systemPrompt;
    }

    private async Task<string> GenerateAsync(Conversation fork, CancellationToken ct)
    {
        using var sampler = new DefaultSamplingPipeline { Temperature = _temperature };
        var decoder = new StreamingTokenDecoder(_executor!.Context);
        var vocab = _weights!.Vocab;
        var sb = new StringBuilder();

        for (var i = 0; i < _maxTokens; i++)
        {
            if (!await InferToReadyAsync(fork, ct)) break;

            var token = fork.Sample(sampler, 0);
            if (token.IsEndOfGeneration(vocab)) break;

            decoder.Add(token);
            sb.Append(decoder.Read());
            if (ContainsAntiPrompt(sb)) break;

            fork.Prompt(token);
        }
        return sb.ToString();
    }

    // A prompt larger than the batch size is queued as several batches, and each
    // Infer() drains only one — so pump until the conversation is ready to sample.
    private async Task<bool> InferToReadyAsync(Conversation conv, CancellationToken ct)
    {
        while (conv.RequiresInference)
        {
            var result = await _executor!.Infer(ct);
            if (result != DecodeResult.Ok)
            {
                _logger.LogWarning("LlamaSharp decode returned {Result}; stopping generation early", result);
                return false;
            }
        }
        return true;
    }

    // Gemma has no system role — system content is prepended to the user turn.
    // The shared system prefix ends before the per-listing user text so it stays
    // in the primed KV. Tokenized with special:true so the turn markers map to
    // their control tokens; the model reply ends at <end_of_turn> (an EOG token).
    private static string SystemTurn(string systemPrompt) => "<start_of_turn>user\n" + systemPrompt + "\n\n";

    private static string UserTurn(string userPrompt) => userPrompt + "<end_of_turn>\n<start_of_turn>model\n";

    private static bool ContainsAntiPrompt(StringBuilder sb)
    {
        var text = sb.ToString();
        foreach (var anti in AntiPrompts)
            if (text.Contains(anti, StringComparison.Ordinal)) return true;
        return false;
    }

    private void EnsureLoaded()
    {
        if (_weights is not null && _executor is not null) return;
        lock (_initLock)
        {
            if (_weights is not null && _executor is not null) return;
            var parameters = new ModelParams(_modelPath)
            {
                ContextSize = (uint)_contextSize,
                GpuLayerCount = _gpuLayerCount,
                SeqMax = 2, // base warm prefix + one live fork at a time
            };
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _weights = LLamaWeights.LoadFromFile(parameters);
            _executor = new BatchedExecutor(_weights, parameters);
            watch.Stop();
            _logger.LogInformation(
                "Loaded LlamaSharp model {Path} (context={Ctx}, gpuLayers={Gpu}) in {Ms}ms",
                _modelPath, _contextSize, _gpuLayerCount, watch.ElapsedMilliseconds);
        }
    }

    public void Dispose()
    {
        _baseConv?.Dispose();
        _executor?.Dispose();
        _weights?.Dispose();
    }
}
