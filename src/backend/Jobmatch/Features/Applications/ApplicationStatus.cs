namespace Jobmatch.Features.Applications;

// The status vocabulary is string-shaped like the rest of the marks pipeline
// ("good"/"bad", hyphenated JSON values), so constants + validation instead of an enum.
public static class ApplicationStatus
{
    public const string Applied = "applied";
    public const string Interview = "interview";
    public const string Offer = "offer";
    public const string Rejected = "rejected";
    public const string NoResponse = "no-response";

    public static readonly IReadOnlyList<string> All = [Applied, Interview, Offer, Rejected, NoResponse];

    public static bool IsValid(string value) => All.Contains(value, StringComparer.Ordinal);
}
