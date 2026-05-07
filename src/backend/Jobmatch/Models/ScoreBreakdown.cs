namespace Jobmatch.Models;

/// <summary>
/// Per-component contribution to a Match's final score. Each field is the
/// weighted contribution of that signal — i.e. the raw component score
/// already multiplied by the configured weight from ranking.yml. Summing all
/// seven fields yields the pre-clamp score.
///
/// DisqualifierPenalty is the *delta* applied when a disqualifier keyword
/// fires: it stores how much the pre-penalty sum was reduced (a non-positive
/// number; 0 when no disqualifier hit).
/// </summary>
public sealed record ScoreBreakdown(
    double PrimaryStack,
    double SecondaryStack,
    double Seniority,
    double LocationRemote,
    double Domain,
    double Freshness,
    double DisqualifierPenalty)
{
    public IEnumerable<(string Label, double Value)> EnumerateComponents()
    {
        yield return ("primary_stack", PrimaryStack);
        yield return ("secondary_stack", SecondaryStack);
        yield return ("seniority", Seniority);
        yield return ("location_remote", LocationRemote);
        yield return ("domain", Domain);
        yield return ("freshness", Freshness);
        yield return ("disqualifier_penalty", DisqualifierPenalty);
    }
}
