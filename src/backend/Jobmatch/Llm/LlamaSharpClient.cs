using System.Text;
using LLama;
using LLama.Common;
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
public sealed class LlamaSharpClient : ILlmClient, IDisposable
{
    private readonly string _modelPath;
    private readonly int _contextSize;
    private readonly int _gpuLayerCount;
    private readonly int _maxTokens;
    private readonly float _temperature;
    private readonly ILogger<LlamaSharpClient> _logger;
    private readonly object _initLock = new();
    private LLamaWeights? _weights;
    private LLamaContext? _context;

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
        // Each judgment is independent — clear KV cache so call N+1 starts at position 0
        // instead of inheriting call N's tokens. Without this, llama_decode rejects
        // subsequent batches with "inconsistent sequence positions".
        _context!.NativeHandle.MemoryClear(true);
        var executor = new InteractiveExecutor(_context!);

        var history = new ChatHistory();
        history.AddMessage(AuthorRole.System, systemPrompt);
        var session = new ChatSession(executor, history);

        var inference = new InferenceParams
        {
            MaxTokens = _maxTokens,
            AntiPrompts = new List<string> { "User:", "\nUser", "</user>" },
            SamplingPipeline = new DefaultSamplingPipeline { Temperature = _temperature },
        };

        var sb = new StringBuilder();
        await foreach (var token in session.ChatAsync(
            new ChatHistory.Message(AuthorRole.User, userPrompt),
            inference,
            ct))
        {
            sb.Append(token);
        }
        return sb.ToString();
    }

    private void EnsureLoaded()
    {
        if (_weights is not null && _context is not null) return;
        lock (_initLock)
        {
            if (_weights is not null && _context is not null) return;
            var parameters = new ModelParams(_modelPath)
            {
                ContextSize = (uint)_contextSize,
                GpuLayerCount = _gpuLayerCount,
            };
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _weights = LLamaWeights.LoadFromFile(parameters);
            _context = _weights.CreateContext(parameters);
            watch.Stop();
            _logger.LogInformation(
                "Loaded LlamaSharp model {Path} (context={Ctx}, gpuLayers={Gpu}) in {Ms}ms",
                _modelPath, _contextSize, _gpuLayerCount, watch.ElapsedMilliseconds);
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _weights?.Dispose();
    }
}
