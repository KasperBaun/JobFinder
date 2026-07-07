namespace Jobmatch.Services;

// A mark is "good" or "bad", optionally annotated with a short free-form reason
// ("I'm not a student") that the LLM judge consumes as few-shot signal on later runs.
// The application status tracks what actually happened after applying and lives
// independently of the mark — an entry persists while either is set (R-096).
public sealed record ListingMark(string? Mark, string? Reason, string? Status = null);

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
