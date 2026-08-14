namespace Jobmatch.Services;

// A mark is "good" or "bad", optionally annotated with a short free-form reason
// ("I'm not a student") that the LLM judge consumes as few-shot signal on later runs.
// The application status tracks what actually happened after applying and lives
// independently of the mark — an entry persists while either is set (R-096).
// StatusChangedAt records when the status last changed (R-107); entries recorded
// before timestamps existed stay valid without one.
public sealed record ListingMark(
    string? Mark,
    string? Reason,
    string? Status = null,
    DateTimeOffset? StatusChangedAt = null);
