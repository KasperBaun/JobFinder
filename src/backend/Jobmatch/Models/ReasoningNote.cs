namespace Jobmatch.Models;

/// <summary>
/// One clause of a match's rationale, as a stable key plus the values it interpolates — so the GUI
/// renders it in the user's language instead of receiving finished English prose.
/// <para>
/// The backend keeps ownership of <em>which</em> clause applies: the choice depends on signals that
/// never reach the wire (whether the seniority match was merely adjacent, whether the title gate
/// actually changed the score, how the posting's age compares to the configured half-life), so the
/// frontend could not re-derive it from the DTO.
/// </para>
/// <para>
/// <see cref="Key"/> is persisted in run history and is therefore a contract: add keys, never rename
/// or repurpose one.
/// </para>
/// </summary>
public sealed record ReasoningNote(string Key, IReadOnlyDictionary<string, object>? Args = null);
