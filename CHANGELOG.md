# Changelog

Shipped work, newest first. One line per change — full detail is in the git commit
(and `docs/requirements.md` / `docs/tasks/T-007/` for rationale). Migrated from `todo.md`.

## Recent

- Sources page filter/search — search box + On/Off/Failing chips with live counts; fixed active-chip hover repainting its label invisible.
- Settings "Active profile" section — switch active email / data folder post-setup via the existing setup endpoint (live context swap + query invalidation).
- Overview & Search now agree on "last search" (shared `lastCompletedRun`), interrupted runs are labelled, and the hero compresses on short windows so the stat cards clear the fold.
- Config export/import skips live locked files (`hangfire.db`, `logs/`) — excluded from exports, left in place on import (`IsTransientDir`).
- Merged `client-server-rewrite` into `dev` — async Hangfire job-based observable search, 17 new DK sources, preferred-companies ranking, WARN+ file logging.
- LLM model download: retry + HTTP Range resume, survives transient TLS resets.
- Guided first-run profile wizard — data-location step + skippable essentials profile writes the user's own `skillset.md` (no generic seed). (R-076)
- Windows installer + local build — GitHub Actions builds a self-contained `win-x64` installer (Inno Setup); `npm run package:win` does the same locally. (R-074)
- Config import/export — Settings exports/imports all of `data/<email>/` as a versioned zip, zip-slip guarded, backs up current state first. (R-087, R-088)
- Ship `skillset.example.md` in the build — first-run seeding was silently no-oping, causing 500s on clean install.
- First-run setup (data-location consent) — deferred `UserContext` via `IUserContextProvider`; user confirms email + data folder, persisted to `bootstrap.json`; unconfigured calls return 428. (R-075)
- `LlmModelDownloader` HF SSL renegotiation — set `AllowRenegotiation = true`; verified live end-to-end.
- Source expansion round 2 — Danske Bank (Oracle) unblocked via `JsonValueReader.Walk` numeric segments + 5 DK .NET employers (ids 44-49); favorite-company "★ Favorite" badge.
- Source expansion — 5 RSS query feeds (ids 30-34), 7 favorite-company boards (Workday/Lever/HTML, ids 37-43), manual-import docs; `HtmlAdapter` gained `enrichBody`.
- Preferred companies — `## Preferred companies` skillset section + `preferred_company_boost` ranking multiplier + LLM-prompt hint. (R-091)
- Searches run as durable Hangfire background jobs with a `JobSearch` state machine — survive navigation/reload/host restart; SSE live progress + reconnect; local-only Hangfire dashboard. (R-036/037/038/055)
- DR Teknologi cross-portal duplicate collapsed — `CompanyCanonicalForm` alias map in `Deduper` (`dr → danmarks radio`).
- Cross-portal Jobindex duplicate collapse — split trailing `, <Company>` off RSS titles + normalise dedupe key (legal-form strip, remote-tail strip, district-code drop).
- Host file logging for WARN+ at `data/<email>/logs/host.log` (rolling 5 MB, `NReco.Logging.File`); startup banner prints the path.
- `LlmModelDownloader` SSL renegotiation against HuggingFace — first fix + surfaced `InnerException.Message` in the SSE error event.
- LLM end-to-end smoke test — bumped LLamaSharp 0.21→0.27 (gemma3 support), cleared KV cache per judgment; 6/8 curated examples now in top 10.
- Danish remote-mode keywords in `InferRemoteMode` (`hjemmearbejde`/`hjemmefra` → hybrid, `fjernarbejde` → remote); frontend hides `unknown` badge.
- LLM model auto-download (Phase 2) — `LlmModelDownloader` streams the GGUF with progress; `GET /api/llm/status` + `POST /api/llm/download-model` (SSE) + `<LlmModelBanner />`.
- LLM judging layer (Phase 1) — `Jobmatch.Llm`: `ILlmClient`, in-process `LlamaSharpClient` (Gemma GGUF, default), `OllamaClient`, `LlmJudge`, `ExamplesLoader`; `SearchService` blends LLM + keyword score, off by default.
- HR-Manager.net adapter (CHANGE Lingerie, Danmarks Radio) — parses the server-rendered `PositionList` JSON, follows each ad for the body.
- Jobindex preview location extraction — pull `jix_robotjob--area` into `Location`; Sundhed.dk/DR/Dansk Sundhedssikring jump 0.24-0.35 → 0.49-0.60.
- Jobindex full-text queries — `RssAdapter` honours `QueryParams` (fetch bytes → `ReadFromString`), follows Jobindex "see-job" links; two new query providers.
- TeamTailor adapter (sitemap + JSON-LD) + Danske Spil board + Greater Copenhagen suburb aliases + ISO country-code normalisation.
- Body-fetch enrichment for `ApiAdapter` — `EnrichBody` moved to `BaseAdapter`; SmartRecruiters tenants now rank on full body text (Sopra Steria .Net #1 @ 0.71).
- Five SmartRecruiters DK boards + `CityAliases` (Copenhagen ↔ København/Kbh/Cph) + `max_age_days` 60 → 180. (R-090)
- RSS body enrichment — `enrichBody: true` on `PortalConfig`; `RssAdapter` fetches + strips each item's page into `Description`. (R-089)
- Ranker tuning — `seniority_adjacency_credit` (adjacent = full credit) + `non_engineering_title_multiplier` gate. (R-044)
- Provider toggle bug — symmetric opt-in/opt-out; `ProviderState` gains an explicit `Enabled` list, `IsUserEnabled = enabled ?? (catalog.enabled && !disabled)`. (R-086)
- Pleo migrated from Greenhouse (empty) to Ashby (37 jobs).
- DK feeds enabled by default — `it-jobbank-rss` + `jobsearch-dk` flipped on in the catalog.
- Code-review cleanup pass — cwd-fallback data dir (R-004), host shutdown as `IEndpointRegistration`, `Jobmatch.Json`/`Jobmatch.IO` extraction, `AdapterFactory` registry, Vitest/RTL + Playwright smoke tests.
- Provider catalog moved into the app bundle (`Configuration/portals.json`); per-user state reduced to opt-outs + secrets (`provider-state.json`); one-shot `portals.yml` migration shim. (R-085, R-086)
- Longlist filterable/sortable table (`LonglistTable.tsx`) with URL-hash filter state.
- Production-readiness pass — headless backend driven via curl; `StripHtml` tag-boundary spacing, disqualifiers scoped to title/company, default `top_n` 10 → 25. (R-041)
- `top_jobs.md` carries YAML frontmatter (`generated_at`, `match_count`, `top_score`).
- Removed unused `PortalConfig.BaseUrl` (dead surface across loader/GUI/DTOs).
- `ScoreSeniority` adjacent reasoning text fixed — "Seniority adjacent (near-fit, half credit)." replaces the misleading "Seniority fits."
- `ManualAdapter` CSV — stream parser preserves quoted newlines + doubled-quote escaping.
- Rate limiting honoured by all HTTP adapters — `BaseAdapter.ThrottleAsync` per-instance; `rate_limit_rps: 0` disables. (R-028)
- `ApiAdapter` pagination — `pagination:` block (param/start/step/size/max_pages) loops until empty/partial page. (R-027)
- `ApiAdapter.RenderTemplate` warns on unknown `{key}` placeholders instead of silently dropping the listing.
- Playwright install path resolved at runtime (`AppContext.BaseDirectory`) instead of a hardcoded Debug path.
- `InferSeniority` reads the description as a fallback when the title is silent on level.
- Country → region mapping for cross-EU remotes — EU-27 + EEA + Switzerland match at Region tier; UK excluded post-Brexit.
- `ApiAdapter` POST + endpoint templating — `Method` / `BodyTemplate` fields, `{key}` substitution from `query_params`. (R-026)

## Foundations

- `T-007` — DK portal API research: 5 viable feeds found, dead/manual portals confirmed; disabled stubs + per-portal worksheets under `docs/tasks/T-007/`.
- `T-006` — search transparency: `RunDetail` records raw / dedupe / scored / dropped sections; `ScoreBreakdown`; tabbed run-detail UI. (R-035, R-044, R-032)
- `T-005` — Copenhagen-relevant provider seed; `StaticFields` overlay in `BaseAdapter.BuildListing`. (R-025)
- `T-003`/`T-002` — GUI feature pages (Providers/Skillset/Search-SSE/History/Marks) + scaffold (Kestrel host, `UserContext`, React 19 + Vite).
- GUI v2 — editable Skillset/Providers, navy editorial redesign, shared components.
- Spectre + `Jobmatch.Cli` removed — GUI is the only entry point.
- `data/<email>/examples/` seed-archetype convention + `data/` symlink convention. (R-054, R-055)
- Repo restructure (`docs/` + `src/` + `data/<email>/`), `docs/prd.md`, `docs/requirements.md`.

(Anything older than the restructure lives in git history.)
