using System.Text.Json.Serialization;

namespace Jobmatch.Features.Applications;

/// <summary>What happened after applying. Tracked independently of the mark (R-096); absent is <c>null</c>.</summary>
public enum ApplicationStatus
{
    Applied,
    Interview,
    Offer,
    Rejected,

    // The shared JSON policy camel-cases enum names, which would write "noResponse". The persisted
    // and GUI-facing spelling is hyphenated, so it is declared rather than derived.
    [JsonStringEnumMemberName("no-response")]
    NoResponse,
}

/// <summary>
/// The spellings marks.json and the GUI use. They are persisted, so this map is a contract: new
/// members are fine, changing an existing member's string orphans stored statuses. It exists
/// because the marks codec reads and writes these by hand; a test pins it against what
/// System.Text.Json produces, so the two cannot drift.
/// </summary>
public static class ApplicationStatuses
{
    private static readonly (ApplicationStatus Value, string Wire)[] Map =
    [
        (ApplicationStatus.Applied, "applied"),
        (ApplicationStatus.Interview, "interview"),
        (ApplicationStatus.Offer, "offer"),
        (ApplicationStatus.Rejected, "rejected"),
        (ApplicationStatus.NoResponse, "no-response"),
    ];

    public static IReadOnlyList<string> AllWire { get; } = [.. Map.Select(m => m.Wire)];

    public static string ToWire(this ApplicationStatus value) => Map.First(m => m.Value == value).Wire;

    /// <summary>Null for absent, blank or unrecognised input — callers decide whether that is an error.</summary>
    public static ApplicationStatus? TryParse(string? wire)
    {
        if (string.IsNullOrWhiteSpace(wire)) return null;
        var needle = wire.Trim();
        foreach (var (value, w) in Map)
            if (string.Equals(w, needle, StringComparison.OrdinalIgnoreCase)) return value;
        return null;
    }
}
