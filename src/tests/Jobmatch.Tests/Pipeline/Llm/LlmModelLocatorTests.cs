using Jobmatch.Pipeline.Llm;
using JobmatchUserContext = Jobmatch.Platform.Paths.UserContext;

namespace Jobmatch.Tests.Pipeline.Llm;

/// <summary>
/// Where the model file is, and whether AI can run right now. Two API handlers each carried a copy
/// of this and only one of them checked the file existed, so a CV extraction could be accepted
/// against a model that had never been downloaded.
/// </summary>
public sealed class LlmModelLocatorTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly JobmatchUserContext _ctx;

    public LlmModelLocatorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "llm-locator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _ctx = JobmatchUserContext.Resolve(
            emailOverride: "llm@example.com", repoRoot: _tempRoot, seedExamples: false);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // Only the llm block varies between these tests; the rest is the minimum the loader accepts.
    private const string RankingPreamble = """
        weights:
          primary_stack: 1.0
          secondary_stack: 0.0
          seniority: 0.0
          location_remote: 0.0
          domain: 0.0
          freshness: 0.0

        """;

    private LlmModelLocator Locator(string llmBlock)
    {
        File.WriteAllText(Path.Combine(_ctx.RootDir, "ranking.yml"), RankingPreamble + llmBlock);
        // RankingPath resolves the per-user file first, so re-resolving picks up what was just written.
        return new LlmModelLocator(JobmatchUserContext.For(_ctx.Email, _ctx.RootDir));
    }

    private const string EnabledLlamaSharp = """
        llm:
          enabled: true
          provider: llamasharp
          model_path: models/gemma.gguf
        """;

    [Fact]
    public void ARelativeModelPathResolvesInsideTheUsersDataDirectory()
    {
        var locator = Locator(EnabledLlamaSharp);

        Assert.Equal(Path.Combine(_ctx.RootDir, "models/gemma.gguf"), locator.ModelPath);
    }

    [Fact]
    public void AnAbsoluteModelPathIsUsedAsGiven()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "shared-models", "gemma.gguf");
        var locator = Locator($"""
            llm:
              enabled: true
              provider: llamasharp
              model_path: {absolute}
            """);

        Assert.Equal(absolute, locator.ModelPath);
    }

    [Fact]
    public void EnsureReadyRejectsAMissingModelFile()
    {
        var locator = Locator(EnabledLlamaSharp);

        var ex = Assert.Throws<InvalidRequestException>(locator.EnsureReady);
        Assert.Contains("not been downloaded", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureReadyPassesOnceTheModelIsPresent()
    {
        var locator = Locator(EnabledLlamaSharp);
        Directory.CreateDirectory(Path.GetDirectoryName(locator.ModelPath)!);
        File.WriteAllText(locator.ModelPath, "gguf");

        locator.EnsureReady();
    }

    [Fact]
    public void EnsureReadyRejectsAiBeingSwitchedOff()
    {
        var locator = Locator("""
            llm:
              enabled: false
              provider: llamasharp
              model_path: models/gemma.gguf
            """);

        var ex = Assert.Throws<InvalidRequestException>(locator.EnsureReady);
        Assert.Contains("AI is disabled", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureReadyDoesNotRequireALocalFileForAServedModel()
    {
        // Ollama holds the model itself, so there is nothing on our disk to check for.
        var locator = Locator("""
            llm:
              enabled: true
              provider: ollama
              model_path: models/gemma.gguf
            """);

        locator.EnsureReady();
    }
}
