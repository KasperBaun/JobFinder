namespace Jobmatch.Models;

public sealed record MatchReasoning(
    IReadOnlyList<string> PrimaryStackHits,
    IReadOnlyList<string> SecondaryStackHits,
    IReadOnlyList<string> DomainHits,
    bool? SeniorityMatch,
    bool? LocationMatch,
    bool? RemoteMatch,
    IReadOnlyList<string> DisqualifierHits,
    string Notes,
    // Structured form of Notes. Optional and trailing so runs recorded before it deserialize.
    IReadOnlyList<ReasoningNote>? NoteKeys = null);
