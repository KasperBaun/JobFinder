namespace Jobmatch.Models;

public sealed record MatchReasoning(
    IReadOnlyList<string> PrimaryStackHits,
    IReadOnlyList<string> SecondaryStackHits,
    IReadOnlyList<string> DomainHits,
    bool? SeniorityMatch,
    bool? LocationMatch,
    bool? RemoteMatch,
    IReadOnlyList<string> DisqualifierHits,
    string Notes);
