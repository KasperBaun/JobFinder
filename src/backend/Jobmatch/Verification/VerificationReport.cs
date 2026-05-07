using System.Text;

namespace Jobmatch.Verification;

public sealed record VerificationReport(IReadOnlyList<VerificationCheck> Checks)
{
    public bool HasFailures => Checks.Any(c => c.Status == VerificationStatus.Fail);
    public bool HasWarnings => Checks.Any(c => c.Status == VerificationStatus.Warn);

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Verification report");
        sb.AppendLine();
        sb.AppendLine($"_Generated {DateTimeOffset.Now:O}_");
        sb.AppendLine();
        sb.AppendLine("| Status | Check | Details |");
        sb.AppendLine("|---|---|---|");
        foreach (var c in Checks)
        {
            var icon = c.Status switch
            {
                VerificationStatus.Pass => "✅",
                VerificationStatus.Warn => "⚠️",
                _ => "❌",
            };
            var details = c.Details.Replace("|", @"\|").Replace("\n", " ");
            sb.AppendLine($"| {icon} | {c.Name} | {details} |");
        }
        sb.AppendLine();
        var summary = HasFailures ? "❌ Verification failed." : HasWarnings ? "⚠️ Verification passed with warnings." : "✅ Verification passed.";
        sb.AppendLine(summary);
        return sb.ToString();
    }
}
