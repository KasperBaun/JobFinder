namespace Jobmatch.Models;

/// <summary>
/// Per-component contribution to a Match's final score. Each field is the
/// weighted contribution of that signal — i.e. the raw component score
/// already multiplied by the configured weight from ranking.yml. Summing all
/// fields yields the pre-clamp score.
///
/// DisqualifierPenalty and NonEngineeringTitlePenalty store the *delta*
/// applied when their respective gate fires: each is a non-positive number
/// (0 when the gate didn't fire) representing how much the pre-penalty sum
/// was reduced.
/// </summary>
public sealed record ScoreBreakdown(
    double PrimaryStack,
    double SecondaryStack,
    double Seniority,
    double LocationRemote,
    double Domain,
    double Freshness,
    double DisqualifierPenalty,
    double NonEngineeringTitlePenalty = 0.0)
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
        yield return ("non_engineering_title_penalty", NonEngineeringTitlePenalty);
    }
}
