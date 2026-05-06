# todo

Current status of work on `jobfinder`. Per-task specs live in [`docs/tasks/`](docs/tasks/).

## In progress

_(none)_

## Backlog (next up)

_(none)_

## Completed (recent)

- **`ScoreSeniority` adjacent reasoning text fixed.** Previously a
  Mid-level listing for a Senior user got `Seniority: 0.5` (half
  credit) AND `SeniorityMatch: true`, with reasoning text "Seniority
  fits." — actively misleading. `BuildNotes` now takes the seniority
  score alongside the match flag and emits "Seniority adjacent
  (near-fit, half credit)." for the adjacent case while preserving
  the existing "fits / mismatch / not stated" texts. `MatchReasoning`
  shape unchanged so the GUI / RunDetail JSON / output writers all
  remain wire-compatible. One new test asserts the new text appears
  for adjacent matches and the misleading "Seniority fits" no longer
  does. 132 total green.
- **`ManualAdapter` CSV: quoted newlines preserved.** `ReadCsvFile`
  used `StreamReader.ReadLine` which split inside quoted fields that
  span lines, producing malformed rows. Replaced with a stream-based
  parser (`CsvRow.ParseCsvRecords`) that walks the whole file and
  honours `\n` / `\r\n` inside quotes as part of the field. New test
  exercises a 3-line description quoted across newlines; doubled-quote
  escaping (`""` → `"`) preserved. 131 total green.
- **Rate limiting honoured by all HTTP adapters (R-028).** New
  `BaseAdapter.ThrottleAsync` tracks a per-instance last-call
  timestamp and waits before each HTTP call so the configured
  `rate_limit_rps` is not exceeded. Wired into
  `ApiAdapter.FetchOnePageAsync` (so paginated fetches space their
  pages), `RssAdapter.FetchAsync` (before `FeedReader.ReadAsync`),
  and `HtmlAdapter.FetchInternalAsync` (before `page.GotoAsync`).
  `rate_limit_rps: 0` disables throttling. Test helpers
  (`PaginatedGet`, `JoobleLike`) set rps=0 to keep their multi-call
  test runs fast; one new test asserts the throttle activates
  (3 paged calls at 10 rps elapse >= 180 ms). 130 total green.
- **`ApiAdapter` pagination (R-027).** New `pagination:` config block
  (`param`, `start`, `step`, optional `size_param` + `size`,
  `max_pages` safety cap) loops requests until an empty page, a
  partial page (`count < size`), or `max_pages` is reached. Pagination
  param routes into `body_template` for POST providers and
  `query_params` for GET. `ApiAdapter.FetchAsync` refactored to call a
  per-page helper with a one-shot `RenderEndpointTemplate` upstream
  (so `{api_key}` substitutions happen once, not per page). New
  `PaginationConfig` record. The shipped `careerjet-dk` and `jooble`
  stubs now carry pagination blocks (page-based, 1-indexed,
  `max_pages: 5` capping at 500 / 250 jobs respectively). Six new
  tests (4 adapter + 2 loader); 129 total green.
- **`ApiAdapter.RenderTemplate` warns on unknown keys.** A
  `url_template: "https://example.com/jobs/{Id}"` mapping against an
  item that doesn't carry `Id` used to silently substitute an empty
  string, produce a malformed URL, and drop the listing — no signal to
  the user about why coverage was low. Now logs a warning per item
  with portal + missing key, e.g. `portal=jobnet url_template
  references '{Id}' which is not in this item; the produced URL will
  be malformed and the listing will be dropped`. Drop behaviour
  preserved (one bad item doesn't kill the run). Method moved from
  `static` to instance so it can use `Logger`.
- **Playwright install path resolved at runtime.** `HtmlAdapter` and
  `ConfigVerifier` no longer hardcode `bin/Debug/net10.0/playwright.ps1`
  in their warning messages — the path is now built via
  `Path.Combine(AppContext.BaseDirectory, "playwright.ps1")` so the
  command logged in Release / packaged / non-Debug builds points at the
  actual install location. Shared via `HtmlAdapter.PlaywrightInstallCommand`.
  Notes blocks in `portals.example.yml` (`html-example`, `recruit-it`)
  updated accordingly.
- **`InferSeniority` now reads description as a fallback.** Title
  remains authoritative — when the title carries a seniority keyword
  (jr/junior/graduate/intern, sr/senior, lead/principal/staff,
  mid/intermediate) the description is ignored. When the title is
  silent ("Software Engineer"), the matcher scans the description
  using the same regex set, so a listing with "We're hiring a senior
  backend engineer..." now scores at full seniority weight instead
  of falling to the 0.5 "not stated" half-credit. Two new tests; 123
  total green.
- **Country → region mapping for cross-EU remotes.** Hardcoded EU-27
  + EEA non-EU (Iceland, Norway, Liechtenstein) + Switzerland country
  list in `Ranker.LocationTier`; an EU-region user now matches a
  listing in "Berlin, Germany" / "Amsterdam, Netherlands" / "Prague,
  Czech Republic" etc. at the Region tier (0.3) instead of falling
  through to Else (0.1). Country tier still wins when the listing
  matches the user's own country (regression test for Mikkel/Denmark).
  UK intentionally excluded post-Brexit; users who want UK matches
  declare `Country: "United Kingdom"` explicitly. Two new tests; 121
  total green.
- **`ApiAdapter` POST + endpoint templating extension.** First T-007
  follow-up — unblocks the `jooble` stub. New `Method` and
  `BodyTemplate` fields on `PortalConfig`; loader parses `method:` and
  `body_template:`. `ApiAdapter.FetchAsync` branches on method (GET
  default; POST attaches `JsonContent.Create(BodyTemplate)`),
  validates against `[get, post]`, and substitutes `{key}`
  placeholders in `endpoint` from matching `query_params` entries
  (consumed keys are removed from the query string; unknown
  placeholder throws `ConfigException`). Headers loop now skips
  `Content-Type` so JsonContent can set its own. `jooble` block in
  `portals.example.yml` cleaned of "NOT YET SUPPORTED" markers and
  ships disabled awaiting a user-registered api_key. New R-026.
  Seven new tests (5 adapter + 2 loader); 119 total green; 0 warnings.
- **T-007 — Portal API research (DK general + tech).**
  Surveyed 20 DK general + tech portals for automatable feeds.
  Per-portal worksheets under [`docs/tasks/T-007/`](docs/tasks/T-007/);
  roll-up at [`docs/tasks/T-007/INDEX.md`](docs/tasks/T-007/INDEX.md).
  Dispatched 8 parallel research agents (Jobindex family / public
  sector / newspaper aggregators / EURES / TechJob / Careerjet finish /
  Jooble finish / 5-portal manual+dead survey). Five new viable
  endpoints discovered, three more confirmed dead/manual. New disabled
  stub blocks shipped in `src/config/portals.example.yml` under a
  T-007 section: `jobindex-rss` (undocumented public feed, accepts
  `q=`), `it-jobbank-rss` (same Jobindex backend, IT-scoped),
  `jobsearch-dk` (canonical for the Jobzonen pool, RSS only carries
  title/desc/link), `careerjet-dk` (api, free `affid` registration),
  `jooble` (api, ships disabled with explicit "NOT YET SUPPORTED"
  markers — needs adapter POST/`body_template:`/`{api_key}`
  extension), `recruit-it` (html scrape with stable selectors),
  `stepstone-dk` (manual stub clarifying it doesn't share Jobindex's
  `/jobsoegning.rss`). Confirmed dead: Indeed.dk (single-source XML
  shut off Mar 31 2026), Ofir.dk (whole-host 301 to jobindex.dk),
  Jobzonen.dk (duplicates jobsearch.dk), Monster.dk, Workindenmark.dk
  (frontend over Jobnet/EURES), DevJobsScanner. Confirmed manual:
  Jobnet (STAR cert auth), LinkedIn, Stepstone, Jobbank, EURES (CSRF
  gate), TechJob (robots.txt opts out AI crawlers).
- **T-006 — Search transparency (raw / dedupe / scored / dropped).**
  Every run now records four extra sections in `RunDetail`: raw
  fetched listings per provider, dedupe merge groups, the full
  scored list with per-component breakdown, and dropped listings
  with explicit reasons (`disqualifier`, `below_min_score`,
  `beyond_top_n`, `above_max_age`, `missing_required_primary`).
  Library: new `ScoreBreakdown` record (weighted contributions of
  primary/secondary/seniority/location/domain/freshness, plus a
  `DisqualifierPenalty` delta that's ≤ 0 when triggered);
  `Deduper` returns `DedupeResult { Deduped, Merges }`; `SearchService`
  accumulates raw-by-provider + classifies each non-shortlisted
  match (now also applies `max_age_days` and
  `require_primary_stack_hit` to align with the `Ranker.Filter`
  contract). Server: no new endpoints — `GET /api/history/{runId}`
  flows through the extended `RunDetail` automatically. Client:
  `RunDetailView` grows a tab strip (Shortlist / Raw fetch / Dedupe /
  Full ranking / Dropped); URL-hash-driven so each tab is
  bookmarkable; component bar visualises the per-signal score split;
  reason chips filter the Dropped table. Search page progress rows
  become deep-links into the matching tab once a run completes.
  R-035 + R-044 added; R-032 (raw persistence) finally fulfilled.
  112 tests green. Bundle 322 KB JS / 98 KB gzipped.
- **T-005 — Copenhagen-relevant provider seed.**
  Added `StaticFields` to `PortalConfig` (parsed from a `static_fields:`
  YAML block) and a tiny overlay in `BaseAdapter.BuildListing` —
  static value wins over mapped value when non-empty. Picks up Api /
  Rss / Html adapters since all three call `BuildListing`. New R-025
  documents the contract. Rewrote `portals.example.yml` with five
  verified Greenhouse boards (`pleo`, `trustpilot`, `unity3d`, `wolt`,
  `adyen`), each stamping its company name via `static_fields`. Kept
  `thehub` and `remotive` enabled as complements; added a disabled
  `adzuna-dk` skeleton with placeholder keys + signup notes for the
  Danish aggregator. Each candidate slug was probed live before
  inclusion — many initially-suggested companies (Vivino, Templafy,
  Dixa, Forecast, TwentyThree, Falcon.io, Siteimprove, Zendesk,
  Tradeshift, Lunar) 404'd and were dropped. Loader and adapter tests
  cover round-trip parse + override precedence; a smoke test parses
  the shipped example file end-to-end. 105 tests green.
- **GUI v2 — editable Skillset/Providers + navy editorial redesign.**
  (commit `42f1d44`, no T-NNN — feedback iteration on T-003.)
  `PUT /api/skillset` and `PUT /api/providers` with structured editors,
  dirty tracking, save bar, toast feedback. Providers round-trips
  `portals.yml` so unknown sub-blocks (`html`, `query_params`,
  `headers`, `response_mapping`) survive a save. New design system:
  navy/white/grey palette, all text in deep navy (never black),
  Fraunces display + IBM Plex Sans/Mono, hairline borders, navy
  top-edge accent on cards via `border-top` so it follows the radius.
  Action color unified to navy-800 (matches the score badge). New
  shared components: `TagInput`, `Toggle`, `SaveBar`, `Toast`,
  `[data-tooltip]` CSS pattern. `UserContext` now anchors
  `data/<email>/` to the repo root by walking up to the nearest
  `.git` (fixes `dotnet run --project src/Jobmatch.Gui` creating a
  stray data dir). `ApiAdapter` detects HTML responses and surfaces
  a clear error instead of *"'<' is an invalid start of a value"*.
  `portals.example.yml`: `jobnet` ships disabled, `thehub` and
  `remotive` seeded enabled.
- **T-003 — GUI feature pages.** Providers, Skillset, Search (SSE), History (list + detail), Marks. New `Jobmatch/Search/SearchService` orchestrates the load → fetch → dedupe → rank → write → persist-history pipeline and yields polymorphic progress events. Five endpoint groups + handlers + DTOs; client gains TopNav, dashboard HomePage, four feature pages, MarkButton (optimistic with rollback), useSearchStream. 99 tests green; bundle 296 KB JS / 92 KB gzipped.
- **T-002 — GUI scaffold.** `Jobmatch.Gui` (Kestrel slim host, ephemeral port, `/api/ping` + `/api/shutdown`, SPA fallback, browser auto-launch). `Jobmatch/UserContext` (typed paths under `data/<email>/`, three-source email resolution, first-run example seeding, ranking-override fallback). React 19 + Vite 6 + react-query + react-router-dom client. `BuildGuiClient` MSBuild target gated on `-p:BuildGui=true`. 7 UserContext tests added.
- **Spectre + Jobmatch.Cli removed** — GUI is the only entry point. Deleted `src/Jobmatch.Cli/`, `Spectre.*` package pins, dependent tests (`CliSmokeTests`, `ListingsIntegrationTests`), and `.claude/commands/`. Build green at 86 tests.
- **`data/<email>/examples/` convention** — seed-archetype listings (liked or disliked) live here, one markdown file per listing with YAML frontmatter. Four user-curated examples saved as initial input. Captured as R-054 / R-055.
- **`data/` symlink convention** — typical setup is a junction/symlink to a personal sync folder; data never tracked.
- **Repo restructure** — `docs/`, `src/` (with tests + configs), `data/<email>/`, root `README.md`, `CLAUDE.md`, `todo.md`. Build infra moved under `src/`. Existing user state migrated to `data/tappah2510@gmail.com/`.
- **`docs/prd.md`** — actor + responsibility PRD, no implementation detail.
- **`docs/requirements.md`** — one-line requirement list (`R-001`..`R-084`, plus R-054/R-055).
- **`docs/mwt-tool-analysis.md`** — analysis of the `mwt` GUI/Server pattern jobfinder adopts (GUI half only).

(Anything older than the restructure lives in git history.)

## Pending engine improvements (carried over)

These pre-date the restructure. Still valid; will fold into the GUI work where they touch the same code paths.

- **Remove unused `PortalConfig.BaseUrl`.** Parsed and stored, never read.

## Nice-to-haves

- Live smoke against `jobnet.dk` behind an env flag (off in CI).
- YAML frontmatter at the top of `top_jobs.md` for downstream tooling.
- Markdown table escaping for `|` in titles/companies.

## Bugs deferred

