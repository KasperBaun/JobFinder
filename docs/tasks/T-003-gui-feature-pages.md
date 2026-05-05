# T-003 — GUI feature pages

## Why

The user wants to see what they configured (providers, search criteria), look at past runs, and mark good matches — all from the GUI. Depends on T-001 (per-user paths) and T-002 (GUI scaffolding).

## Outcome

Four working pages in the SPA, backed by four endpoint groups:

| Page | Endpoint group | Purpose |
|---|---|---|
| **Providers** | `/api/providers` | List configured providers, show enabled/disabled, type, last fetch summary |
| **Skillset** | `/api/skillset` | Show the parsed active skillset (stack, location, disqualifiers, weights) |
| **Search** | `/api/search` | Run a search; SSE stream of per-provider progress; render results |
| **History** | `/api/history` | List past runs with timestamp, listings count, and good-match count |

Plus a **mark** action on the Search and History pages: click "good match" on a listing → POSTs `/api/marks` → persisted under `data/<email>/marks.json`.

## Scope

### Server
- `Server/Endpoints/ProvidersEndpoints.cs`, `SkillsetEndpoints.cs`, `SearchEndpoints.cs`, `HistoryEndpoints.cs`, `MarksEndpoints.cs`.
- Matching handlers under `Server/Handlers/`.
- DTOs under `Server/Models/`.
- `Search` handler runs the existing pipeline as an `IAsyncEnumerable<StepEvent>` over SSE — one event per provider fetch start / done / failed, plus dedupe / rank / write events.
- After a successful run, persist a `data/<email>/history/<run-id>.json` summary with: timestamp, providers, fetched count, deduped count, ranked count, top score, shortlist length.

### Client
- New pages under `Client/src/pages/`: `ProvidersPage`, `SkillsetPage`, `SearchPage`, `HistoryPage`.
- Top nav linking the four pages plus Home.
- React Query for the GETs; `useStepStream` hook (lifted from mwt) for the search SSE.
- Mark button on listings: optimistic update, POST to `/api/marks`, rollback on failure.
- History page shows a small chart or count column: "marked good / total shortlist".

## Out of scope

- Editing skillset or providers from the GUI in this pass — read-only views; users still edit the underlying files. (Editing is a future task.)
- Algorithm changes that *use* the marks data — feedback collection only; learning loop is later.

## Acceptance

- All four pages render against a populated `data/<email>/`.
- Running a search from the GUI matches the CLI's output for the same skillset/providers (compare `top_jobs.md`).
- Marking a listing persists; refreshing keeps the mark.
- History page reflects new runs without restart.

## Requirements covered

R-020, R-023, R-030, R-033, R-050, R-051, R-052, R-053.
