namespace Jobmatch.Infrastructure.Llm;

/// <summary>
/// A GGUF the in-process backend loads. Where the file goes and where it comes from travel
/// together — the download endpoint needs both, and neither is useful alone.
/// </summary>
public sealed record LlmModelFile(string ConfiguredPath, Uri DownloadUrl)
{
    /// <summary>Gemma 3 4B Instruct at Q4_K_M — public, no auth, ~2.3 GB. What jobfinder ships with.</summary>
    public static LlmModelFile ShippedDefault { get; } = new(
        "models/gemma-3-4b-it-q4_k_m.gguf",
        new Uri("https://huggingface.co/mradermacher/gemma-3-4b-it-GGUF/resolve/main/gemma-3-4b-it.Q4_K_M.gguf"));

    // A relative ConfiguredPath resolves against the user's own data directory, so the shipped
    // default lands at data/<email>/models/gemma-3-4b-it-q4_k_m.gguf.
    public string AbsolutePath(string userDataDir) =>
        Path.IsPathRooted(ConfiguredPath) ? ConfiguredPath : Path.Combine(userDataDir, ConfiguredPath);
}
