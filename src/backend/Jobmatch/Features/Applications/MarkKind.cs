namespace Jobmatch.Features.Applications;

/// <summary>Whether a listing turned out to be a fit. Absence of a mark is <c>null</c>, not a member.</summary>
public enum MarkKind { Good, Bad }

/// <summary>
/// The spellings marks.json and the GUI use. They are persisted, so this map is a contract: new
/// members are fine, changing an existing member's string orphans every mark already on disk. It
/// exists because the marks codec reads and writes these by hand; a test pins it against what
/// System.Text.Json produces, so the two cannot drift.
/// </summary>
public static class MarkKinds
{
    private static readonly (MarkKind Value, string Wire)[] Map =
    [
        (MarkKind.Good, "good"),
        (MarkKind.Bad, "bad"),
    ];

    public static string ToWire(this MarkKind value) => Map.First(m => m.Value == value).Wire;

    /// <summary>Null for absent, blank or unrecognised input — callers decide whether that is an error.</summary>
    public static MarkKind? TryParse(string? wire)
    {
        if (string.IsNullOrWhiteSpace(wire)) return null;
        var needle = wire.Trim();
        foreach (var (value, w) in Map)
            if (string.Equals(w, needle, StringComparison.OrdinalIgnoreCase)) return value;
        return null;
    }
}
