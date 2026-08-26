namespace Jobmatch.Infrastructure.Llm;

/// <summary>
/// How to reach a language model, parsed from the <c>llm:</c> block of ranking.yml. Everything
/// specific to one backend lives on <see cref="LlmProvider"/>, so there is no field here that is
/// meaningless for the provider in use. How far the judge's verdict moves a score is ranking
/// policy, not plumbing — that is <c>JudgeConfig</c>.
/// </summary>
public sealed record LlmConfig(bool Enabled, LlmProvider Provider, double Temperature)
{
    /// <summary>What an absent <c>llm:</c> block means, and the source of every per-key default.</summary>
    public static LlmConfig Disabled { get; } = new(
        Enabled: false,
        Provider: LlmProvider.LlamaSharp.Defaults,
        Temperature: 0.0);
}
