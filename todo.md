# todo

Current status of work on `jobfinder`.

## Backlog (next up)

- **Recruit IT html scrape — Playwright `:scope` smoke test.** The disabled
  `recruit-it` portal stub uses `:scope` as `link_selector` to target the
  wrapping `<a>`. Verify Playwright resolves `:scope` inside
  `IElementHandle.QuerySelectorAsync` before flipping `enabled: true`;
  otherwise extend `HtmlAdapter` to read attributes from the matched list
  element directly.
- **Recruit IT location parsing.** Location renders as a plain text node
  next to an icon with no wrapper class; `location_selector` is intentionally
  omitted and listings will have null location until the markup changes.
- **`jobindex-rss` / `it-jobbank-rss` dedupe warning.** Both feeds hit the
  same Jobindex backend; enabling both wastes calls. Add a config-time hint
  or a UX warning when both are enabled at once.
- **`jobsearch-dk` company/location parser.** Items expose only `title` /
  `description` / `link` — no `pubDate`, no structured company/location.
  Add a parser that extracts company/location from the title or URL slug
  `/{role}/{city}/{id}`.
- **Remove migration shim.** `PortalsMigrationShim.RunIfNeeded` runs on every
  Gui startup. After all known users have run the new build at least once,
  delete the shim, its tests, and the YAML loader's only remaining caller path.

## In progress

- **Provider expansion to actually-DK employers (Phase 1 of "fix the
  corpus before LLM scoring").** The 5 Greenhouse boards we have are
  international tech with DK office (Unity / Wolt / Adyen / Trustpilot
  / Pleo). The user's actual market — DK consultancies, public-sector
  IT, regulated industries — sits on different ATS systems entirely.
  This commit added 5 SmartRecruiters DK boards (Sopra Steria,
  Netcompany, Deloitte Nordic, Devoteam, BEUMER). Still needed:
  TeamTailor adapter (covers Danske Spil + many DK startups),
  Emply / HR-Manager.net adapters (covers Sundhed.dk, Dansk
  Sundhedssikring, DR Teknologi, CHANGE Lingerie),
  Jobindex.dk full-text search (umbrella DK aggregator), and
  body-fetch for ApiAdapter (SmartRecruiters list endpoint doesn't
  include description, so Netcompany roles score 0.41 on title alone
  but get dropped by `require_primary_stack_hit`).

- **LLM-based judging via Ollama + Gemma 3 4B (Phase 2).** Once the
  corpus contains the right kind of jobs, swap the keyword ranker for
  (or augment with) an LLM judge that scores every deduped listing
  using the user's skillset + curated examples as few-shot signal.
  User has indicated this is the direction once provider work lands.

## Completed (recent)

- **Jobindex preview location extraction — Sundhed.dk / DR / Dansk
  Sundhedssikring jump from 0.24-0.35 to 0.49-0.60.** Jobindex
  preview pages embed the location in
  `<span class="jix_robotjob--area">København Ø</span>`. Body
  enrichment now extracts that into the listing's `Location` field
  (when not already set), so Jobindex-sourced listings get City
  tier instead of falling through to ELSE. Dramatic uplift on the
  maintainer's run: top score 0.71 → 0.74 (Sopra Steria via Jobindex
  edges past Sopra Steria via SmartRecruiters), DR Teknologi #3 at
  0.60, Sundhed.dk + Dansk Sundhedssikring both at 0.49 (just below
  the top 15 cutoff).

- **Jobindex full-text queries — two new RSS providers + RssAdapter
  query-param + Jobindex preview "see-job" follow.** RssAdapter
  previously ignored `Config.QueryParams` entirely (FeedReader's
  `ReadAsync` strips literal `+` characters from query strings, which
  broke any AND query on Jobindex). Three fixes: (a) RssAdapter now
  fetches the feed bytes via HttpClient and hands them to
  `FeedReader.ReadFromString` so the encoded query survives;
  (b) `AppendQueryParams` moved to BaseAdapter so RssAdapter shares
  ApiAdapter's URI-assembly logic; (c) for Jobindex preview pages
  (`jobindex.dk/vis-job/*`), the body-enrichment step now follows the
  embedded "see-job" link to fetch the employer's actual ATS posting
  rather than the Jobindex wrapper.
  Two new catalog entries: `jobindex-rss-softwareudvikler`
  (`q=+softwareudvikler`) and `jobindex-rss-net-udvikler`
  (`q=+.net +udvikler`). The old single-query `jobindex-rss` and the
  it-jobbank-rss `q=developer` both retuned for relevance.
  `jobsearch-dk` disabled — its RSS feed is category-index pages
  (Receptionist, Tjener, Maler), not real openings (confirmed
  2026-05-11). Outcome on the maintainer's run: DR Teknologi
  example surfaces at 0.35 (was missing entirely), Sundhed.dk +
  Dansk Sundhedssikring surface at 0.24-0.27 (still title-only since
  HR-Manager.net pages need a dedicated adapter — coming next).

- **TeamTailor adapter (sitemap + JSON-LD) + Danske Spil board +
  Greater Copenhagen suburb aliases + ISO country normalisation.**
  Third batch of "fix the corpus" provider work. New
  `TeamTailorAdapter` parses sitemap.xml for job URLs, fetches each
  page, extracts the schema.org JobPosting JSON-LD blob, maps to
  Listing. No API key needed — works for any TeamTailor career site.
  Danske Spil added as the seed tenant (5 listings). The adapter
  also normalises ISO 3166-1 alpha-2 country codes (DK→Denmark etc.)
  since TeamTailor stores `addressCountry` as the short code, which
  the ranker's substring matcher missed. `CityAliases` extended:
  Greater Copenhagen / Storkøbenhavn / Hovedstaden now expand to the
  14 real Greater Copenhagen suburb municipalities (Brøndby,
  Albertslund, Ballerup, Dragør, Gentofte, Gladsaxe, Glostrup, Herlev,
  Hvidovre, Høje-Taastrup, Ishøj, Lyngby-Taarbæk, Rødovre, Tårnby,
  Vallensbæk) so a listing in any of these earns the Metro tier for a
  user who declared "Greater Copenhagen" or "Hovedstaden". Outcome:
  Danske Spil's "Softwareudvikler" example moved from 0.32 → 0.50
  (just outside top 10; bottleneck is now "Seniority not stated"
  because the Danish title carries no level marker). New
  `PortalType.TeamTailor`, wired into AdapterFactory. 198 backend
  tests green (+1 Brøndby alias test).

- **Body-fetch enrichment for ApiAdapter — Netcompany & friends now
  rank with full body text.** Previous commit added 5 SmartRecruiters
  DK boards but only Sopra Steria's .Net role surfaced (#4 at 0.46);
  the other 80+ DK listings (47 Netcompany, 15 Deloitte Nordic, 8
  Devoteam, 13 BEUMER) scored well on title alone (0.40-0.42) but got
  dropped by `require_primary_stack_hit` because the SmartRecruiters
  list endpoint omits descriptions. This commit extracts the
  body-fetch helpers from `RssAdapter` to `BaseAdapter` (sequential
  ~5rps fetch + StripHtml + merge into Description) and wires
  `EnrichBody: true` on all 5 SmartRecruiters tenants. Result:
  Sopra Steria .Net is now #1 at **0.71** (up from 0.46), top 25
  is 18× SmartRecruiters DK consultancy roles, only 2 Greenhouse
  entries (Unity Commerce, Trustpilot QA) survived. Total run time
  ~95s (was 65s) — the +30s is body-fetch latency for ~88 SR
  postings. 197 backend tests still green (helpers moved, not
  changed; existing RssAdapter tests still call them via inheritance).

- **Five SmartRecruiters DK boards + Copenhagen alias + age cutoff
  loosened.** First batch of "fix the corpus" provider work.
  (a) Added catalog entries for SopraSteria1, Netcompany1,
  DeloitteNordic, Devoteam, BEUMERGroup1 — all filtered to
  `country=dk`, all using the existing ApiAdapter via `url_template`.
  ~88 DK listings added across the five tenants. Sopra Steria's
  "Senior .Net udvikler" example now lands at #4 in the shortlist
  (0.46), up from "not in corpus at all". (b) New `CityAliases`
  table in `Ranker.LocationTier` that matches Copenhagen ↔ København
  / Kbh / Cph and a few other DK city pairs across language variants;
  fixes the 0.6 → 1.0 location-tier jump for any DK-language listing.
  (c) `max_age_days` bumped 60 → 180 in the bundled ranking.yml; the
  freshness signal still soft-decays older listings, but B2B/consulting
  roles often stay open 60-120 days. R-090 added; R-044 unchanged.
  Two new alias tests; 197 backend tests green (was 195).

- **RSS body enrichment — DK feeds get a real corpus.** New
  `enrichBody: true` field on `PortalConfig` (parsed from the catalog
  `enrichBody` key). When set, `RssAdapter` walks the feed items
  sequentially after parsing, fetches each item's linked HTML page
  (~5rps), strips the markup, and merges the body text into
  `Listing.Description` so ranker stack-keyword matching has a real
  corpus to work against. Failures don't drop the listing — RSS-only
  version is kept, warning logged. Both DK feeds shipped with the flag
  on (`it-jobbank-rss`, `jobsearch-dk`); other RSS-typed portals stay
  off by default. Closes the last item from the 2026-05-11 quality pass.
  R-089 added. 8 new RssAdapter tests; 195 backend tests green
  (was 187).

- **Ranker tuning — adjacent seniority full-credit + non-engineering
  title gate.** Two changes addressing the "top score caps at 0.49"
  observation. (a) `seniority_adjacency_credit` (RankingConfig +
  ranking.yml, default 1.0) — adjacent seniority (mid↔senior, etc.)
  now scores the same as exact match. The IT market overcounts
  "Senior", so penalising adjacency was dragging down most real
  matches for mid-with-experience users. Notes still distinguish
  "adjacent" from "fits". (b) `non_engineering_title_multiplier`
  (default 0.2) — listings whose title looks clearly non-engineering
  (Product/Project/Account Manager, Marketing/Sales/Finance/Customer/
  Recruit/HR roles, Data/Business/Fraud Analyst, etc.) get their
  score multiplied down. Engineer/Engineering/Developer/Architect/
  SRE/DevOps in the title overrides the gate (so QA Engineer,
  Software Engineering Manager, DevOps Lead all pass through). New
  `NonEngineeringTitlePenalty` field in `ScoreBreakdown` so the audit
  view shows the delta. R-044 updated, R-087/R-088 added. 7 new
  tests; 187 backend tests green (was 180).

- **Provider toggle bug — symmetric opt-in/opt-out.** Discovered while
  enabling the DK feeds: the GUI toggle in `/providers` was silently no-op
  for any provider where the catalog defaulted to `enabled: false`. The
  merger did `catalogPortal.Enabled && !state.Disabled.Contains(id)`, so
  flipping the user-state toggle on a catalog-disabled provider only
  affected the opt-out side and did nothing. Fixed by adding an explicit
  `Enabled` opt-in list to `ProviderState` (alongside the existing
  `Disabled` opt-out list); `IsUserEnabled` now resolves to `enabled
  ?? (catalog.enabled && !disabled)`. `ProvidersService.SetEnabled`
  branches on `catalog.Enabled` to manipulate the right list:
  catalog-on toggles flip Disabled, catalog-off toggles flip Enabled.
  Backwards compatible — old state files without `enabled` load with an
  empty opt-in list, no behaviour change. R-086 updated. Three new
  tests (EnabledIdInState_OverridesCatalogDisabled, EnabledOptInBeats
  DisabledOptOut_WhenBothPresent, LoadOrEmpty_LegacyFileWithoutEnabled
  Field_LoadsAsEmptyEnabled). 180 backend tests green (was 177).

- **Pleo migrated from Greenhouse (empty) to Ashby (37 jobs).**
  Greenhouse Pleo board returned 200 with empty `jobs` array — Pleo
  moved their hiring to Ashby. Switched id=1 to
  `api.ashbyhq.com/posting-api/job-board/pleo` with one-to-one field
  mapping (id, title, location, descriptionHtml, jobUrl, publishedAt).
  Renamed `greenhouse-pleo` → `pleo`. Test comment updated; existing
  run history that references `greenhouse-pleo` stays as historical
  data. Closes one of the four follow-ups from the 2026-05-11 quality
  pass.

- **DK feeds enabled by default in the catalog.** `it-jobbank-rss` (id 15) and
  `jobsearch-dk` (id 16) flipped from `enabled: false` to `enabled: true` in
  `src/backend/Jobmatch/Configuration/portals.json`. Both were already
  validated by the providers' Test endpoint (25 and 100 listings). Adding
  them takes the active corpus from 783 → 940 raw / 777 → 850 deduped on
  the maintainer machine, surfaces real Danish-language listings (best new
  match: Everllence Software Engineer @ 0.37 via it-jobbank-rss). Discovery
  triggered the four follow-up items above. The Jobindex (RSS) (id 14)
  catalog default is intentionally left at `false` — same backend pool as
  it-jobbank, would just waste calls.

- **Code-review cleanup pass (K-G1 / K-G2 / K-G3 / K-C1 + SOLID/DRY full pass).**
  Resolved six review findings and a focused SOLID/DRY pass.
  - `760829b` K-C1: `scripts/dev-utils.mjs` — stale `Jobmatch.Gui` comment + `/api/shutdown` path corrected to `Jobmatch.Host` + `/api/system/shutdown`.
  - `7da0473` K-G1: `UserContext.Resolve` now falls back to `%LOCALAPPDATA%/jobfinder/` (Windows) / `~/.local/share/jobfinder/` (Unix) when run outside a git repo, instead of dumping `data/<email>/` cwd-relative. New requirement R-004; new `cwdOverride` test seam; throws `ConfigException` if `LocalApplicationData` resolves empty.
  - `37f82e9` K-G2: host shutdown moved from inline `app.MapPost` in `Program.cs` into `Jobmatch.Host/Endpoints/HostShutdownEndpoint : IEndpointRegistration`, matching the API-layer pattern. Standalone `Jobmatch.Api` continues to NOT expose shutdown (codified by canary test below).
  - `7c3debe` SOLID/DRY: extracted `Jobmatch.Json/{JobmatchJsonOptions,JsonValueReader,StringTemplate}` and removed duplicated JSON options + `JsonElement→string` + `{key}` template logic from `SearchService`, `SseHelper`, `ApiAdapter`, `ManualAdapter`. No behaviour change.
  - `0be3759` SOLID/DIP: introduced `Jobmatch.IO/{IFileSystem,PhysicalFileSystem}` and injected through `ManualAdapter` / `AdapterFactory.Create` / `SearchService` / `ProvidersService`. DI registers singleton. Remaining `File.*` / `Directory.*` calls in `SearchService.WriteHistory` and `ProvidersService.LoadLastFetchByProvider/LoadRecentRuns` are deliberately untouched — out of scope for the audit's DIP finding (which targeted `ManualAdapter`); seam is plumbed if future tests need it.
  - `2c88391` SOLID/OCP: `AdapterFactory` switch replaced by `Dictionary<PortalType, Factory>` registry. Audit also suggested a `KnownTypes` export consumed by `PortalCatalogLoader` and `SearchService`; on inspection neither file actually enumerates portal types, so that speculative export was skipped (CLAUDE.md no-premature-abstraction rule).
  - `3f8ad43` K-G3 backend: added `Microsoft.AspNetCore.Mvc.Testing` and `SystemEndpointsTests` covering ping=200 and shutdown=404-on-standalone-Api. The Api project already declares `<StartupObject>Jobmatch.Api.ApiProgram</StartupObject>` so we use `WebApplicationFactory<ApiProgram>` directly — no `partial class Program;` shim needed.
  - `146792d` K-G3 frontend: added Vitest + RTL + jsdom and one `App.test.tsx` smoke test (wraps `App` in `MemoryRouter` + `QueryClientProvider`). Root `npm run test:client` now runs Vitest instead of the build.
  - `2872de8` K-G3 e2e: added `src/tests/playwright/tests/system.spec.ts` with two specs (ping=200, SPA `#root` mounts). Not auto-run; user runs locally via `npm run test:e2e`.
  - K-S1 (path-traversal probe): closed — no fix needed. Probe rejected cleanly via lookup-not-concat in `ProvidersHandler`. No leak.
  - K-C3 (email mismatch): closed — auto-memory under `~/.claude/projects/.../memory/` contains no email; the system-reminder `userEmail` field comes from a global user setting outside this project's memory. Code reads dynamically from `git config user.email` (`UserContext.TryGetGitUserEmail`), so no repo or memory edit applies.

  Final test counts: backend 177/177 (was 174), frontend 1/1 new, Playwright 2 specs scaffolded.

- **Provider catalog moved into app bundle; per-user state reduced to opt-outs + secrets.**
  Replaced `data/<email>/portals.yml` (per-user, gitignored, drift-prone) with
  `src/backend/Jobmatch/Configuration/portals.json` (committed catalog) +
  `data/<email>/provider-state.json` (opt-out ids and secrets). Removes the
  existing-user portal migration gap entirely. One-shot startup shim
  (`PortalsMigrationShim.RunIfNeeded`) translates any legacy `portals.yml`
  into the new state file and renames the yaml `.bak`. GUI loses the
  +Add/Edit/Delete affordances; the toggle and a per-provider secrets form
  remain. New requirements R-085, R-086. Resolves the long-standing
  "existing-user portal migration" backlog item.

- **Longlist filterable table.** Replaced the small ranking-table on the
  History run-detail's Longlist tab with `LonglistTable.tsx`, a
  filterable/sortable view (search, portal chips, posted-within, score
  range, stack-hit chips, mark, shortlist-only). Filter state lives in the
  URL hash so it's bookmarkable and survives refresh. `ScoredEntry` extended
  with `primaryStackHits`/`secondaryStackHits` so the stack-hit filter has
  data to filter on.

- **Production-readiness pass: backend testable headless, three quality
  fixes shipped.** Drove the existing Kestrel `/api/search` SSE endpoint
  with `curl` (no React UI involved) to iterate on real output for the
  active user. Three fixes: (1) `BaseAdapter.StripHtml` now inserts a
  space at each tag boundary so `<p>experience with</p><ul><li>TypeScript`
  no longer collapses to `experience withTypeScript` — silently broke
  word-boundary keyword matching for every thehub listing (0/15 ranked
  before, ~3/15 ranked after). (2) `Ranker` disqualifiers now scope to
  title + company only, not description — `Lead a team of
  junior-to-senior engineers` no longer zeroes real Senior roles, and
  users can blacklist marketplaces by company name (e.g. `Lemon.io`).
  R-041 updated. (3) Default `top_n` bumped 10 → 25 so the long tail of
  Senior matches stops being dropped to `beyond_top_n` after coverage
  scales. 137 tests green (was 132); commits `0e7f2e7`, `dc58089`,
  `9cae83f`. T-007 stubs migrated into the active user's portals.yml
  (gitignored, not committed): 5 Greenhouse boards + `it-jobbank-rss` +
  `jobsearch-dk` enabled, taking enabled provider count 2 → 9 and
  fetched listings 38 → 897 per run.

- **`top_jobs.md` carries YAML frontmatter for downstream tooling.**
  Each generated `top_jobs.md` now opens with a small `---` block
  carrying `generated_at`, `match_count`, and `top_score` so static-
  site / pipeline tools can read the report without scraping the H1
  back. New test asserts the frontmatter precedes the `# Top matches`
  heading and contains the expected fields. Stale nice-to-haves
  pruned: `jobnet.dk` live smoke (T-007 confirmed STAR cert-auth is
  the only path), markdown `|` escaping in titles (no rendered table
  carries title/company text — only label/score pairs).
- **Removed unused `PortalConfig.BaseUrl`.** The field was parsed
  from `base_url:` YAML, round-tripped through the GUI editor, and
  never read by any adapter — pure dead surface area. Dropped from
  `PortalConfig`, `PortalConfigLoader`, the GUI's `ProviderSummary` /
  `ProviderUpsert` DTOs, the `ProvidersHandler` Get/Put paths
  (including the absolute-URL validation), the TypeScript
  `ProviderSummary` / `ProviderUpsert` shapes, and the editor field
  in `ProvidersPage.tsx`. Existing user YAMLs that happen to carry
  `base_url:` survive — the loader now ignores it; the GUI's
  preserve-unknown-keys round-trip leaves it alone in the file.
  Frontend bundle 321 KB JS / 98 KB gzipped; 132 tests still green.
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
  stray data dir, no longer relevant after move to src/backend/Jobmatch.Api). `ApiAdapter` detects HTML responses and surfaces
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

(Anything older than the restructure lives in git history.)

## Nice-to-haves

_(none — table-escaping nice-to-have was stale; the rendered table only carries label/score pairs, no titles/companies that could contain `|`. Live smoke against `jobnet.dk` is no longer worth chasing — T-007 confirmed STAR cert-auth is the only path. Frontmatter shipped.)_
