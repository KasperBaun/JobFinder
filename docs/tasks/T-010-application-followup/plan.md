# T-010 — Application follow-up (R-107)

Tester feedback: wants to keep track of applications "now that so many companies choose
not to answer". Application tracking itself already shipped (R-096/R-097/R-098:
per-listing status applied/interview/offer/rejected/no-response + the cross-run
Applications view). This task closes the follow-up gaps the tester actually hits.

## Scope

1. **Timestamp on status changes.** `ListingMark` (Jobmatch/Services/ListingMark.cs)
   gains optional `DateTimeOffset? StatusChangedAt`; `MarksService.SetStatus` stamps it
   on every status change (clock injectable or `TimeProvider` for tests). Persisted
   additively in the marks.json object shape as `statusAt` — entries without it stay
   valid (both legacy shapes — bare string and object — keep parsing; `Project()`
   keeps collapsing to the bare string only when reason+status+statusAt are all null).
2. **Applications view upgrades** (`ApplicationsPage` + backend
   `ApplicationsService`/`ApplicationsResponse`):
   - "Status set" column, formatted via `i18n/format.ts` (`dateTimeFormat` /
     `relativeTimeFormat`) — never bare `toLocaleString`; sortable.
   - Stat tiles: counts per status ("12 applied · 3 interviews · 1 offer").
   - Status filter (client-side is fine; the payload is small).
3. **Awaiting-response badge.** Listings with status `applied` whose `statusChangedAt`
   is older than 14 days (frontend constant) get a "waiting N days" badge — pure
   render-time derivation, NO scheduler/notification (repo constraint: no background
   schedulers).
4. **Robustness fix.** `ApplicationsService.List()` currently `break`s when a run's
   history JSON is missing — change to skip, so a pruned run doesn't hide the remaining
   applications.
5. **Wire-up.** `HistoryService` run-detail merge exposes the timestamp
   (`markStatusAt` map alongside `markStatuses`); frontend `api/types.ts`
   (`ApplicationEntry.statusChangedAt?`, `RunDetail.markStatusAt?`); i18n
   `applications` namespace en+da in the same change (labels, tiles, badge,
   filter — `satisfies Record<ApplicationStatus, string>` where enum-keyed).

## Out of scope

Status history/transition log, a status note separate from the mark reason, a status
filter in the longlist. Take later if requested.

## Docs

R-107 in `docs/requirements.md`; one CHANGELOG line; drop the T-010 entry from todo.md.

Draft R-107:

> **R-107** The system should record when a listing's application status last changed
> and surface it on the Applications view — a sortable "status set" column, per-status
> totals, a status filter, and a visible "awaiting response" indicator on applied
> listings unchanged for more than 14 days — derived purely at render time (no
> scheduler). Status entries recorded before timestamps existed remain valid without
> one, and a pruned history run must not hide the remaining tracked applications.

## Tests

- `MarksServiceTests` — SetStatus stamps/updates `StatusChangedAt`; clearing status
  clears it; legacy entries (bare string / object without `statusAt`) load; persistence
  round-trip keeps the additive field.
- `ApplicationsServiceTests` — timestamp flows through; newest-run-wins still holds;
  missing run JSON is skipped, later runs still listed.
- `ApplicationsEndpointsTests` — response carries `statusChangedAt`.
- `HistoryServiceMergeTests` — `markStatusAt` merged into run detail.
- Vitest — stat tiles counts, status filter, awaiting-response badge threshold logic,
  date column uses locale formatters; catalog parity enforced by existing tests.
