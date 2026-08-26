# Jobmatch

The backbone library: the domain, the search engine, the other user capabilities, the plumbing.
`Jobmatch.Api` wraps it in HTTP; `Jobmatch.Host` composes it into a runnable bundle.

```text
Domain/          the nouns — Listing, Match, Skillset, and Runs/ (a run's persisted record)
Search/          the verb  — one search run, phase by phase
Features/        the other verbs — Providers, Skillsets, History, Applications, Cv,
                 Drafting, Transfer, Bootstrap, AiModel
Infrastructure/  the plumbing — Paths/, IO/, Json/, Llm/
Exceptions.cs    namespace `Jobmatch`, so every thrower reaches it without a using
```

Two rules hold the shape:

- **Namespace equals folder path.** `Search/Fetching/Adapters/` is `Jobmatch.Search.Fetching.Adapters`. No exceptions, so the two cannot drift.
- **One entry point per phase folder.** `SearchRunner` calls exactly one type per folder under `Search/`; the rest is that phase's business.

---

## The run

`Search/SearchRunner.cs` sequences the phases below and yields the progress events the GUI streams.
Each folder is one phase, named for the phase it performs.

| Folder | Entry point | Persisted `JobSearchPhase` | What the user reads |
|---|---|---|---|
| `Planning/` | `RunPlanner.Plan` | `Pending` | Search started |
| `Fetching/` | `ProviderFetch.FetchAll` | `Fetching` | Fetching listings from N sources |
| `Deduplication/` | `DuplicateMerger.Merge` | `Deduping` | N unique jobs after removing duplicates |
| `Ranking/` | `Ranker.Score` | `Ranking` | N jobs rated |
| `Judging/` | `AiReview.JudgeUntilShortlistStable` | `LlmJudging` | AI reviewing top N jobs |
| `Ranking/` | `ShortlistBuilder.BuildShortlist` | `Ranking` | — |
| `Recording/` | `RunRecorder.Record` | `Writing` | Writing results |

`Ranking/` appears twice on purpose: scoring runs before the AI judge, the shortlist cut after it.

`Locations/` is the one folder under `Search/` that is not a phase. It holds the gazetteer, the
distance maths and the radius filter, which both dedupe and ranking use.

---

## Where things are not

| Looking for | It lives in | Why |
|---|---|---|
| Talking to a language model | `Infrastructure/Llm/` | The CV extractor uses it too, so it is not a search step |
| Getting the model onto the machine | `Features/AiModel/` | A user capability behind `/api/llm/*`, never touched by a run |
| Who the user is, where their data lives | `Features/Bootstrap/` | First-run setup, data directory, UI language |
| The `portals.yml` parser | `Features/Providers/Legacy/` | Retired format; only the one-shot migration reads it |
| Writing a resume for a listing | `Features/Drafting/` | Starts after a run ends, from the ad text the run stored |
| The user's CV text | `Features/Cv/` | Read to prefill a profile, kept because drafting writes from it |

**See:** [`../../../CLAUDE.md`](../../../CLAUDE.md) for the repo layout, and the
`dotnet-backend-standards` skill for the API conventions.
