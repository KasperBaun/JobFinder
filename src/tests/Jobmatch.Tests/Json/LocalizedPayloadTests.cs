using System.Text.Json;
using Jobmatch.Domain.Runs;
using Jobmatch.Domain;
using Jobmatch.Platform.Json;

namespace Jobmatch.Tests.Json;

/// <summary>
/// Timeline entries, drop reasons and match rationale now travel as key + args so the GUI can
/// render them in the user's language. Both halves of that have to hold on disk: runs recorded
/// before the keys existed must still load, and the args must survive a read/write round-trip
/// with their JSON types intact (a score has to stay a number, or Danish can't format it as 0,82).
/// </summary>
public sealed class LocalizedPayloadTests
{
    private static T RoundTrip<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JobmatchJsonOptions.Default), JobmatchJsonOptions.Default)!;

    [Fact]
    public void Timeline_Entry_Written_Before_The_Keys_Existed_Still_Loads()
    {
        const string legacy = """
            {"timestamp":"2026-07-06T09:00:00+00:00","level":"info","phase":"fetching",
             "message":"Fetching listings from 3 sources","provider":null,"count":null,"durationMs":null}
            """;

        var evt = JsonSerializer.Deserialize<JobSearchEvent>(legacy, JobmatchJsonOptions.Default)!;

        Assert.Equal("Fetching listings from 3 sources", evt.Message);
        Assert.Null(evt.MessageKey);
        Assert.Null(evt.Args);
    }

    [Fact]
    public void Legacy_Entries_Serialize_Back_Without_Gaining_Empty_Fields()
    {
        var evt = new JobSearchEvent(DateTimeOffset.UtcNow, JobSearchEventLevel.Info, JobSearchPhase.Fetching, "Search started");

        var json = JsonSerializer.Serialize(evt, JobmatchJsonOptions.Default);

        Assert.DoesNotContain("messageKey", json);
        Assert.DoesNotContain("args", json);
    }

    [Fact]
    public void Timeline_Args_Keep_Their_Json_Types_Across_A_Round_Trip()
    {
        var evt = new JobSearchEvent(
            DateTimeOffset.UtcNow, JobSearchEventLevel.Info, JobSearchPhase.Ranking,
            "42 jobs rated · best 0.87", MessageKey: "ranked",
            Args: new Dictionary<string, object> { ["count"] = 42, ["topScore"] = 0.87 });

        var json = JsonSerializer.Serialize(RoundTrip(evt), JobmatchJsonOptions.Default);

        // Numbers must not come back quoted — the GUI formats them per locale.
        Assert.Contains("\"count\":42", json);
        Assert.Contains("\"topScore\":0.87", json);
    }

    [Fact]
    public void Match_Rationale_Round_Trips_As_Keys_And_Args()
    {
        var match = new ListingMatch(
            Id: "1", Portal: "test", Title: "Senior .NET Engineer", Company: "TestCo", Location: "Copenhagen",
            RemoteMode: "hybrid", Url: "https://example.com/1", PostedAt: null, Score: 0.87,
            Reasoning: "Must-have skill match: C#.",
            PrimaryStackHits: ["C#"], SecondaryStackHits: [],
            ReasoningNotes: [new ReasoningNote("primaryHits", new Dictionary<string, object> { ["skills"] = new[] { "C#" } })]);

        var restored = RoundTrip(match);

        Assert.Equal("primaryHits", restored.ReasoningNotes!.Single().Key);
        // The prose stays populated as the fallback for clients and reports that want English.
        Assert.Equal("Must-have skill match: C#.", restored.Reasoning);
    }

    [Fact]
    public void Match_Recorded_Before_The_Notes_Existed_Still_Loads()
    {
        const string legacy = """
            {"id":"1","portal":"test","title":"Senior .NET Engineer","remoteMode":"hybrid",
             "url":"https://example.com/1","score":0.87,"reasoning":"Must-have skill match: C#.",
             "primaryStackHits":["C#"],"secondaryStackHits":[]}
            """;

        var match = JsonSerializer.Deserialize<ListingMatch>(legacy, JobmatchJsonOptions.Default)!;

        Assert.Null(match.ReasoningNotes);
        Assert.Equal("Must-have skill match: C#.", match.Reasoning);
    }

    [Fact]
    public void Llm_Verdict_Fields_Round_Trip_And_Stay_Numeric()
    {
        var match = new ListingMatch(
            Id: "1", Portal: "test", Title: "Senior .NET Engineer", Company: "TestCo", Location: "Copenhagen",
            RemoteMode: "hybrid", Url: "https://example.com/1", PostedAt: null, Score: 0.87,
            Reasoning: "Must-have skill match: C#. AI review: 0.82 — solid fit",
            PrimaryStackHits: ["C#"], SecondaryStackHits: [],
            LlmScore: 0.82, LlmReason: "solid fit");

        var restored = RoundTrip(match);
        var json = JsonSerializer.Serialize(restored, JobmatchJsonOptions.Default);

        Assert.Equal(0.82, restored.LlmScore);
        Assert.Equal("solid fit", restored.LlmReason);
        Assert.Contains("\"llmScore\":0.82", json);
    }

    [Fact]
    public void Match_Recorded_Before_The_Llm_Fields_Still_Loads_And_Serializes_Without_Them()
    {
        const string legacy = """
            {"id":"1","portal":"test","title":"Senior .NET Engineer","remoteMode":"hybrid",
             "url":"https://example.com/1","score":0.87,"reasoning":"Must-have skill match: C#.",
             "primaryStackHits":["C#"],"secondaryStackHits":[]}
            """;

        var match = JsonSerializer.Deserialize<ListingMatch>(legacy, JobmatchJsonOptions.Default)!;

        Assert.Null(match.LlmScore);
        Assert.Null(match.LlmReason);
        Assert.DoesNotContain("llmScore", JsonSerializer.Serialize(match, JobmatchJsonOptions.Default));
    }

    [Fact]
    public void Dropped_Entry_Recorded_Before_Context_Args_Still_Loads()
    {
        const string legacy = """
            {"id":"1","title":"Frontend Developer","company":"TestCo","score":0.31,
             "reason":"below_min_score","context":"score 0.31 below threshold 0.50"}
            """;

        var dropped = JsonSerializer.Deserialize<DroppedEntry>(legacy, JobmatchJsonOptions.Default)!;

        Assert.Null(dropped.ContextArgs);
        Assert.Equal("below_min_score", dropped.Reason);
    }
}
