# Providers as bundled JSON + Longlist filter table

**Date:** 2026-05-06
**Author:** brainstorm session with Kasper
**Status:** approved, ready for implementation plan

## Problem

Two unrelated friction points have stacked up.

1. **Providers are per-user but shouldn't be.** The catalog (`portals.yml`) lives under `data/<email>/`, seeded once from `src/config/portals.example.yml`. Existing users never pick up new portals shipped after their first run (todo.md:27). The mental model says "users curate their own portal list," but in practice nobody will — they'll never get past the empty state. The catalog should ship with the app, ready to go, structured so it can drop into a database row-per-provider when this becomes a multi-user web app.
2. **Longlist is unusable beyond the top 25.** The shortlist (top-N, capped at 25) is fine as cards. The longlist is everything else — typically 800–1100 listings post-dedupe per run — and the current view is a small table with sort by `score / title / company` and no filters. There's no way to ask "show me all Python jobs at companies I've good-marked, posted this week."

## Non-goals

- Multi-user auth / accounts. Single-user today; the JSON shape is *DB-ready* but we don't migrate to a DB in this change.
- Touching the shortlist view. Cards stay. Top-N stays at 25.
- Touching adapters, ranker, dedupe, search pipeline, history persistence.
- Building an "advanced query language" for filters. Chips + slider + text search is the surface.

## Design

### 1. Provider catalog

**Source of truth (committed):**
- `src/Jobmatch/Configuration/portals.json` — the catalog. Replaces `src/config/portals.example.yml`. Read-only at runtime. Copied into the published bundle (`AppContext.BaseDirectory/portals.json`).
- Format: 1-to-1 of the existing YAML — `id`, `name`, `type`, `enabled` (default for the catalog), `endpoint`, `query_params`, `headers`, `body_template`, `method`, `response_mapping`, `static_fields`, `pagination`, `rate_limit_rps`, `notes`. Keys are camelCase in the JSON (`responseMapping`, `staticFields`, `rateLimitRps`) since C# `System.Text.Json` defaults align with that and TS does too.
- One new field: `requiresSecret: string | null` — the secret key name (`api_key`, `affid`) the user must supply for this provider to fetch. `null` for providers that need nothing.

**Per-user overlay (gitignored):**
- `data/<email>/provider-state.json`, default contents `{ "disabled": [], "secrets": {} }`.
  - `disabled: number[]` — provider ids the user has opted out of.
  - `secrets: { [providerId: string]: { [secretName: string]: string } }` — values populated via the GUI secrets form.
- Auto-created empty on first launch.

**Effective enabled rule:**
A provider is fetched on a search iff:
1. Its catalog `enabled !== false`, AND
2. its `id` is not in the user's `disabled[]` list, AND
3. if `requiresSecret` is set, the user has a non-empty value for it in `secrets[id]`.

Providers that pass (1) and (2) but fail (3) show in the GUI as **enabled but missing key** — they don't fetch silently, they surface an "Add key →" link.

**Migration shim (first run after upgrade):**
- If `data/<email>/portals.yml` exists, parse it; for any portal whose `enabled: false`, copy that provider's `id` into `provider-state.json.disabled[]`. Secrets currently inline in YAML (rare — only present if the user has manually populated `jooble`) are copied into `provider-state.json.secrets`.
- Rename `portals.yml` → `portals.yml.bak` (one-shot, leave the backup so nothing is lost).
- Log: `[migration] migrated portals.yml → provider-state.json (N disabled, M secrets); backup at portals.yml.bak`.
- The shim runs once. A follow-up release (not this one) deletes the shim code path.

### 2. GUI: providers

`ProvidersPage.tsx`:
- Tile grid stays. **Edit** button gone, **+ Add provider** button gone.
- Tile shows the existing fields read-only (name, type, endpoint host, last fetch, health), plus a small "needs key" badge for `requiresSecret` providers without a value.
- Toggle (enable/disable) stays — writes to `provider-state.json.disabled[]` via the same `PUT /api/providers/{id}` shape, but the only field that matters now is `enabled`. Backend validates everything else is unchanged.

`ProviderDetailPage.tsx`:
- Catalog fields read-only.
- New **Secrets** form for any provider with `requiresSecret`. Single text input per declared secret, masked. Save button writes `data/<email>/provider-state.json`.
- Test button stays.

### 3. Backend changes

`PortalConfigLoader`:
- New: `LoadCatalog()` reads `AppContext.BaseDirectory/portals.json`.
- New: `ProviderState.Load(path)` reads `data/<email>/provider-state.json`.
- New: merge step. Returns the same `IReadOnlyList<PortalConfig>` shape downstream code already consumes; `Enabled` reflects the overlay; resolved secrets are substituted into endpoint/query_params/body_template via the existing `{key}` placeholder mechanism (R-026).
- The YAML loader stays only behind the migration-shim code path. New tests cover the JSON catalog and the overlay.

`ProvidersHandler`:
- `GetList`, `GetOne`, `Test` stay.
- `Update` (PUT) keeps the same route but reads **only** `enabled` from the request body. All other fields (name, type, endpoint, rateLimitRps, notes) are silently ignored — the catalog is read-only. Deleting the endpoint outright would break the existing optimistic-update flow on `ProvidersPage`; cheaper to keep the route and narrow its surface.
- `Create` (POST), `Delete` (DELETE) — return `405 Method Not Allowed`. Tests for these handlers deleted.
- New: `PUT /api/providers/{id}/secrets` — `{ [name: string]: string }` body. Writes the user's `provider-state.json`. Empty string = clear that secret.

### 4. ScoredEntry stack hits

`ScoredEntry` (record under `Jobmatch/Models`) gains:
```csharp
public IReadOnlyList<string> PrimaryStackHits { get; init; }
public IReadOnlyList<string> SecondaryStackHits { get; init; }
```
The `Ranker` already computes these arrays when building each `ListingMatch` for the shortlist. Stop discarding them when building `ScoredEntry`. RunDetail JSON automatically carries the new fields. Frontend `types.ts` mirrors. Without this the "Stack hit" filter on the longlist has nothing to filter on.

### 5. GUI: longlist filterable table

Replaces `RankingTab` in `src/Jobmatch.Gui/Client/src/pages/HistoryPage.tsx`. New component, likely extracted to `components/LonglistTable.tsx` (HistoryPage.tsx is already 732 lines — pulling this out matches the boundary improvements the brainstorm checklist calls for).

**Layout, top to bottom:**

1. **Filter bar** (sticky under the tab strip), all live, no apply button:
   - **Search** — text input, leftmost, debounced 100ms, case-insensitive substring match across `title + company` (no fuzzy/Levenshtein). ESC clears.
   - **Portal** — multi-select chip cluster, one chip per portal with ≥1 listing in this run, with count.
   - **Posted** — pill group: `24h / 7d / 14d / 30d / any`. Default `any`.
   - **Score** — dual-thumb slider 0.00–1.00, two-decimal readout. Default `0.00 – 1.00`.
   - **Stack hit** — chip cluster of the user's primary + secondary stacks. Each chip shows count. Multi-select, OR semantics. Primary chips visually distinct from secondary.
   - **Mark** — segmented: `all / good / bad / unmarked`. Default `all`.
   - **Shortlist** — toggle: `shortlist-only / all`. Default `all`. (Means: `id ∈ data.shortlist`.)
   - **Reset** — text link, visible only when any filter is non-default.

2. **Result strip** — single line: `247 of 1,203 · sorted by score ↓`.

3. **Table:**
   - Columns: Title (link → listing URL) · Company · Portal (badge) · Location · Posted (relative) · Score (number + breakdown bar, existing visual preserved) · Mark (inline thumbs ↑/↓, optimistic, same backend as the cards) · ⌄ expand (row expands inline showing per-component breakdown, existing behavior preserved).
   - Sortable: title, company, portal, location, posted, score. (Mark is a filter, not a sort key — most rows are unmarked, so sort by mark is noisy.) Click toggles asc/desc. Active sort: column tinted, arrow visible. Default: `score desc`.

4. **Empty state** — `No listings match these filters.` + Reset link.

**URL hash state:**
Existing pattern: `#tab=longlist`. Extends to:
```
#tab=longlist&q=python&portal=greenhouse-pleo,greenhouse-wolt&posted=7d&score=0.45-1&stack=typescript,react&mark=unmarked&shortlist=true&sort=posted-desc
```
Bookmarkable, survives refresh. When loaded against a different run, filters that reference now-missing values (e.g. a portal that didn't run this time) silently drop.

**Performance:**
Filter + sort runs client-side on the existing `data.scored` array (≤ ~1500 rows in practice). React `useMemo` keyed on filter state. No virtualisation in v1; revisit if a run produces >5k post-dedupe listings.

## Requirements impact

- **R-020** ("let a user list providers… enable or disable each without removing it") — still satisfied; the disable surface stays. The catalog moves into the app rather than being user-curated, which the requirement allows.
- **R-024** ("ship a generic example provider list") — the shipped catalog *is* the list now; example file goes away. Update R-024 to reflect that.
- **R-035** (per-listing drop reason) — unchanged. Dropped tab stays.
- **New requirement:** R-085 — *The system should expose a filterable, sortable view of every deduped listing in a run, with at least: portal, score range, posting age, primary/secondary stack hit, mark state, shortlist membership, and free-text search across title and company.*
- **New requirement:** R-086 — *The system should ship the provider catalog as part of the application bundle (read-only at runtime), and store only per-user opt-outs and provider secrets under `data/<email>/`.*

## Open questions / explicit deferrals

- Secrets are stored plaintext in `provider-state.json`. Acceptable for a local-only single-user app (R-080). Worth revisiting when this becomes multi-user.
- The migration shim ships once, runs once per existing user, and stays in the codebase for one release. A follow-up task removes it.
- "Compare two runs side-by-side" is a different feature; not in scope here.

## Test plan

New tests (xUnit, under `src/Jobmatch.Tests/`):
- `ProvidersJsonCatalogTests` — round-trip the new JSON shape; assert all current YAML examples convert; field-by-field equivalence.
- `ProviderStateTests` — load empty / disabled-only / secrets-only; merge with catalog produces correct effective enabled flag.
- `ProvidersHandlerSecretsTests` — `PUT /api/providers/{id}/secrets` writes the file, masks empty values, rejects unknown secret names.
- `ScoredEntryStackHitsTests` — Ranker propagates stack hits into ScoredEntry; RunDetail JSON carries the new fields.
- `MigrationShimTests` — `portals.yml` with a mixed `enabled` set produces correct `provider-state.json`; `.bak` is left behind; idempotent on re-run.

Frontend (Vitest if already wired; otherwise reviewed manually):
- Longlist URL hash round-trip — encode → parse → encode is stable.
- Filter combinations on a fixture run yield expected counts.

Manual verification (skill: verification-before-completion):
- Single binary launches; first run on a clean `data/<email>/` produces a working search with all bundled providers.
- Toggle a provider off → re-search → that provider absent from results.
- Add `jooble` `api_key` via secrets form → re-search → jooble fetches.
- Open longlist on a real run, verify each filter narrows results live and URL hash updates.
