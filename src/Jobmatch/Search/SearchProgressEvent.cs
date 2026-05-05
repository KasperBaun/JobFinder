using System.Text.Json.Serialization;

namespace Jobmatch.Search;

/// <summary>
/// Polymorphic progress event emitted while a search is running. The wire shape uses
/// a `type` discriminator so the SPA can route by event type.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StartedEvent), "started")]
[JsonDerivedType(typeof(ProviderRunningEvent), "provider_running")]
[JsonDerivedType(typeof(ProviderDoneEvent), "provider_done")]
[JsonDerivedType(typeof(ProviderFailedEvent), "provider_failed")]
[JsonDerivedType(typeof(DedupeEvent), "dedupe")]
[JsonDerivedType(typeof(RankEvent), "rank")]
[JsonDerivedType(typeof(CompleteEvent), "complete")]
[JsonDerivedType(typeof(ErrorEvent), "error")]
public abstract record SearchProgressEvent;

public sealed record StartedEvent(int Total) : SearchProgressEvent;

public sealed record ProviderRunningEvent(string Provider, int Index, int Total) : SearchProgressEvent;

public sealed record ProviderDoneEvent(string Provider, int FetchedCount, int Index, int Total) : SearchProgressEvent;

public sealed record ProviderFailedEvent(string Provider, string Error, int Index, int Total) : SearchProgressEvent;

public sealed record DedupeEvent(int MergedCount) : SearchProgressEvent;

public sealed record RankEvent(int RankedCount, double TopScore) : SearchProgressEvent;

public sealed record CompleteEvent(string RunId, IReadOnlyList<ListingMatch> Shortlist) : SearchProgressEvent;

public sealed record ErrorEvent(string Message) : SearchProgressEvent;
