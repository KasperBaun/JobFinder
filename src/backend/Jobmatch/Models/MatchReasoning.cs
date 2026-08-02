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
    IReadOnlyList<ReasoningNote>? NoteKeys = null,
    // The LLM judge's verdict, structured so the GUI can render it as its own row; the same text
    // also rides Notes as "AI review: …" for top-jobs.md. Optional and trailing (older runs).
    double? LlmScore = null,
    string? LlmReason = null);
