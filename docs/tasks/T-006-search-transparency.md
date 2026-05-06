# T-006 — Search transparency (raw / dedupe / scored / dropped)

## Why

The user wants to *trust* the ranking. Today the post-search progress
panel and the run-detail page show summary numbers (e.g. *37 fetched →
37 deduped → 10 ranked*) but no way to inspect what each step did or
why a listing was excluded. Stated motivation: *"build trust in this
algorithm so I can confidently believe it will provide the best results
and not leave any out."*

## Outcome

For any completed run, the user can drill into four extra sections in
the History detail view (and reach them by clicking the corresponding
progress row on the Search page):

| Section | Answers |
|---|---|
| **Raw fetch (per provider)** | What did each portal return, before dedupe? |
| **Dedupe groups** | Which originals merged into which canonical listing? |
| **Full ranking** | Every scored listing — not just the shortlist — with a per-component score breakdown. |
| **Dropped** | Every listing that didn't reach the shortlist, with the explicit reason (disqualifier match, score below threshold, beyond top-N, above max-age, missing required primary stack). |

## Scope

### Library

- `Jobmatch.Ranking.Match` (or a sibling type) gains a
  `ScoreBreakdown` record exposing the weighted contribution of each
  ranking component:
  `PrimaryStack, SecondaryStack, Seniority, LocationRemote, Domain, Freshness, DisqualifierPenalty`
  (each a `double`). `Ranker.Score` populates this alongside the
  existing `Reasoning`.
- `Jobmatch.Search.SearchService` accumulates richer state during a
  run:
  - `Dictionary<string, IReadOnlyList<Listing>> rawByProvider`
  - `IReadOnlyList<DedupeGroup> dedupeMerges` — `{ canonicalId, mergedFromIds[] }`
  - `IReadOnlyList<ScoredEntry> scoredAll` — all post-dedupe listings with
    score + breakdown
  - `IReadOnlyList<DroppedEntry> dropped` — `{ listingId, title, score, reason, context }` where
    `reason ∈ { disqualifier, below_min_score, beyond_top_n, above_max_age, missing_required_primary }`
- `Deduper` returns the merge groups (currently it just returns the
  deduped list). Internal API change; tests need updating.
- `RunDetail` (the on-disk JSON written under `data/<email>/history/`)
  gains optional sections: `raw`, `dedupeMerges`, `scored`, `dropped`.
  Old run files lack these — frontend treats absence as "not available
  for this run."

### Server

- No new endpoints. Extend `GET /api/history/{runId}` to include the
  new sections (read straight from the persisted JSON).
- DTOs under `Server/Models/` mirror the library shapes.

### Client

- `HistoryPage` `RunDetailView` grows a tab strip below the run-summary
  card:
  `[ Shortlist ] [ Raw fetch ] [ Dedupe ] [ Full ranking ] [ Dropped ]`
  Default tab = Shortlist (current behaviour preserved).
- *Raw fetch* — collapsible group per provider, each row shows
  title / company / location / portal-id / url, with the count in the
  group header.
- *Dedupe* — one row per group; shows the canonical listing and the
  N originals that merged into it (linkable).
- *Full ranking* — sortable table: title, company, score, plus a small
  inline bar segmenting the per-component contributions (use the
  existing pill colours; no new chart lib). Expandable row reveals the
  full breakdown.
- *Dropped* — table with title, score, and a reason badge. Filter chips
  at the top to focus on one reason at a time.
- *Search page* — once `stream.status === 'complete'`, make the
  progress rows links: clicking the `thehub ok 15 fetched` row
  navigates to `/history/{runId}#raw=thehub`; `dedupe` row →
  `#tab=dedupe`; `rank` row → `#tab=ranking`. Use URL hash so each
  tab is bookmarkable / refresh-stable.

### Tests

- `RankerTests` — new assertions on `ScoreBreakdown` for a known input
  (sum of components within rounding of total score).
- `DeduperTests` — merge groups returned cover all inputs exactly once.
- `SearchServiceTests` — a run with a known disqualifier + threshold
  produces the right counts in `dropped` with correct reasons.

## Out of scope

- **Manual override / re-pinning** — letting the user pull a dropped
  listing into the shortlist anyway. Worth a follow-up (call it T-007)
  but not here.
- **Re-ranking without re-fetching** — R-032 already calls for raw
  persistence; this task delivers the *persistence* half but not a
  re-rank action. The next task can wire the action once the data is
  there.
- **Marks → ranking feedback loop** — the existing `Mark` flow records
  good/bad but doesn't feed the ranker yet (R-054/R-055 example
  convention). Tackle separately.
- Storing description text twice (once raw, once on the scored entry)
  is wasteful. If on-disk size becomes a problem, dedupe by storing raw
  only and referencing by id from the other sections. Worth measuring
  first; don't pre-optimise.

## Requirements touched

- **R-032** *(persist raw per-provider results so a run can be
  re-examined or re-ranked without re-fetching)* — finally fulfilled
  by writing `raw` into `RunDetail`.
- **(new) R-0XX** *The system should expose, per run, every dropped
  listing and the explicit reason it was dropped.*
- **(new) R-0XX** *The system should record a per-component score
  breakdown for every ranked listing.*

Add the two new R lines to `docs/requirements.md` when this task
opens.

## UX notes

- Reason copy must be plain and specific. Bad: *"low score"*. Good:
  *"score 0.18 below threshold 0.25"*. Each reason should be a short
  imperative the user can act on (raise their threshold, remove a
  disqualifier, etc.).
- The component bar on the ranking table is the most novel piece of
  visual design in the project so far — keep it small (max 200 px),
  one row per listing, hairline borders, navy ramp. Don't make it the
  star; the star is the *number*.
