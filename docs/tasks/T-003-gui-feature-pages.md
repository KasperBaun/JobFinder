# T-003 — GUI feature pages

## Why

The user wants to see what they configured (providers, search criteria), look at past runs, and mark good matches — all from the GUI. Depends on T-002 (GUI scaffold + UserContext).

## Outcome

Four feature pages in the SPA, backed by five endpoint groups:

| Page | Endpoint group | Purpose |
|---|---|---|
| **Providers** | `/api/providers` | List configured providers, show enabled/disabled, type, last-fetch summary |
| **Skillset** | `/api/skillset` | Show the parsed active skillset (stack, location, disqualifiers, weights) |
| **Search** | `/api/search` (SSE) | Run a search; stream per-provider progress; render results |
| **History** | `/api/history` | List past runs with timestamp, listings count, good-match count |
| (mark action) | `/api/marks` | Persist a "good match" toggle per listing per run |

## Scope

### Server

- New `Server/Endpoints/{Providers,Skillset,Search,History,Marks}Endpoints.cs`.
- Matching handlers under `Server/Handlers/`.
- DTOs under `Server/Models/`.
- A new `Jobmatch.Search.SearchService` in the library that orchestrates load → fetch → dedupe → rank → write. The GUI search handler invokes it and streams `IAsyncEnumerable<SearchProgressEvent>` over SSE.
- After a successful search, persist a `data/<email>/history/<run-id>.json` summary: timestamp, providers (with status), fetched count, deduped count, ranked count, top score, shortlist length.
- `marks.json` shape: `{ "<runId>": { "<listingId>": "good" | "bad" } }`. Atomic writes via temp file + rename.

### Client

- New pages under `Client/src/pages/`: `ProvidersPage`, `SkillsetPage`, `SearchPage`, `HistoryPage`.
- Top nav linking Home + the four pages.
- React Query for GETs; a `useSearchStream` hook that consumes the SSE stream from `/api/search`.
- Mark control on listings: optimistic update, POST `/api/marks`, rollback on failure.
- History page shows per-run "marked good / total shortlist" counts.

## Out of scope

- Editing skillset or providers from the GUI — read-only this pass; users still edit the underlying files. (Editing is a future task.)
- Algorithm changes that *use* the marks data — feedback collection only; the learning loop is a separate task.

## Acceptance

- All four pages render against a populated `data/<email>/`.
- A search run from the GUI streams progress per provider and lands a top-N shortlist plus a `history/<run-id>.json` entry.
- Marking a listing persists across refresh.
- History page reflects new runs without restart.

## Requirements covered

R-020, R-023, R-030, R-031, R-032, R-033, R-040, R-041, R-042, R-050, R-051, R-052, R-053.
