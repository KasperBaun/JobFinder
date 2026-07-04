# Masterplan — rules-adherence remediation for the background-jobs refactor

Audit of the Hangfire background-search refactor against `src/backend/rules/`, plus the chunked
remediation dispatched across agents. **The refactor is largely compliant** with the rules that
actually apply to jobfinder — this masterplan covers only the genuine, applicable gaps.

## Why most "violations" are non-issues

`src/backend/rules/` was written for **"Mwt," a multi-tenant SaaS** (JWT auth, EF Core + SQL Server,
GUID entity IDs, permission-based authz). Jobfinder is **local, single-user, file-based, no-auth**.
The following are **deliberate, documented exceptions**, not violations — they are being *codified*,
not "fixed":

- No auth / no `.RequirePermission()` (may be added later)
- File-based JSON store instead of EF Core + migrations
- SQLite instead of SQL Server for Hangfire storage
- String timestamp run-ids instead of GUID primary keys
- Hangfire dashboard local-only / unsecured

### Already compliant (no action)
Layering (Endpoint→Handler→Service), `HandlerBase`/`ExecuteAsync`, `I…Handler` interfaces, full
OpenAPI metadata + typed `Routes.*`, immutable records, test sizes within limits, tests mirror the
source tree. `JobSearchHandler.Stream()` bypassing `ExecuteAsync` is a justified SSE exception.

## Findings (the work)

| # | Finding | Rule | Severity | Origin |
|---|---------|------|----------|--------|
| F1 | `SearchService.cs` = 404 lines (> 300 hard limit; `RunAsync` ~131 lines > 50) | maintainability, refactoring-strategy | High | Pre-existing |
| F2 | `IHistoryService.cs` holds interface **and** ~175-line `HistoryService` impl | one-concern-per-file | Medium | Touched by refactor |
| F3 | No integration test for the durability path (enqueue → run → SSE → terminal → reconnect) | integration-tests | Med-High | Refactor gap |
| F4 | `[AutomaticRetry(Attempts = 1)]` deviates from background-jobs' `Attempts = 3`; undocumented | background-jobs | Low-Med | Refactor |
| F5 | `SseHelper.cs` at API root vs `Infrastructure/`; string-typed `Status`/`Level` vs enum; `SearchJob.Apply` ~51 lines | coding-conventions, maintainability | Low | Mixed |
| F6 | `CLAUDE.md` carve-out too coarse — hides which rules bind vs which exceptions are intentional | — | Medium | — |

Out of scope: frontend file sizes (`HistoryPage.tsx` 616, `SearchPage.tsx` 263) — rules are
backend-only.

## Chunks

Files are **disjoint** across chunks. Every backend agent invokes the `dotnet-backend-standards`
skill and **preserves public API surface** (no behavior change) so the 273-test suite stays green.
Agents **edit only — they do not run the full solution build** (avoids concurrent `bin`/`obj`
collisions); a single consolidated verification pass runs after all chunks land.

### Chunk A — Governance (docs, `CLAUDE.md`) [F6, F4]
Replace the blanket "Auth/EF/Hangfire rules do not apply" with a precise **Adopted rules vs
deliberate exceptions** note. Structural/quality rules apply; auth deferred; EF→JSON, SQL
Server→SQLite, GUID→string-id, dashboard local-only are intentional; background-jobs.md applies
except storage/dashboard. Record the F4 decision: keep `Attempts = 1` (full re-run is expensive,
per-provider failures already handled) with rationale.

### Chunk B — Split `SearchService.cs` [F1]
Partial classes (refactoring-strategy.md), identical `ISearchService` surface:
- `SearchService.cs` — ctor + `RunAsync` orchestration overloads
- `SearchService.Llm.cs` — `JudgeAndBlend`
- `SearchService.Ranking.cs` — `ClassifyDrop`, `BuildDroppedEntry`
- `SearchService.Mapping.cs` — `ToListingMatch` / `ToRawListing` / `ToScoredEntry`
- `SearchService.History.cs` — `WriteHistory`

Main file < 300 lines; methods ≤ 50 where the async-iterator allows.

### Chunk C — Split `IHistoryService.cs` [F2]
Keep `IHistoryService` + `HistoryDeleteResult` in `IHistoryService.cs`; move `HistoryService` class
to new `HistoryService.cs` (same namespace, DI unchanged).

### Chunk D — Integration tests for durability [F3] (after B & C)
WebApplicationFactory-based + a Hangfire-enabled factory variant (`ApiTestFactory` disables jobs).
Cover: `JobSearchService.Create` → invoke `SearchJob.Run` with a `FakeSearchService` → assert
`JobSearchBus` emits running→…→terminal → `Active()` reconnect → `Cancel` marks cancelled. Async
timing waits per integration-tests.md.

### Chunk E — Low-priority cleanups [F5] (optional)
Move `SseHelper.cs` → `Jobmatch.Api/Infrastructure/`; optional `Status`/`Level` enums; optional
`SearchJob.Apply` split. Drop anything that risks the green suite.

## Orchestration
1. Dispatch A, B, C, E in parallel (file-disjoint, edit-only).
2. Dispatch D once B & C land.
3. Consolidated verification.

## Verification
- `dotnet build src/Jobmatch.slnx` — clean (warnings-as-errors).
- `dotnet test src/tests/Jobmatch.Tests` — all existing + new tests green; D's tests run (not skipped).
- `npm --prefix src/frontend run build` — still typechecks.
- Spot-check: `SearchService.cs` & `HistoryService.cs` < 300 lines; `IHistoryService.cs` = interface
  + result record only; `CLAUDE.md` coherent and consistent with "Things to avoid."
