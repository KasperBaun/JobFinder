# todo

Current status of work on `jobfinder`. Per-task specs live in [`docs/tasks/`](docs/tasks/).

## In progress

_(none — T-003 landed; next is whatever comes after a real-world test of the GUI)_

## Backlog (next up)

_(empty — see Pending engine improvements + Nice-to-haves below for smaller items)_

## Completed (recent)

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

- **Country → region mapping for cross-EU remotes.** A listing in "Germany" for an EU-region user falls to else tier (0.1) because the matcher only recognises "EU/Europe/EMEA" synonyms, not member states. Add a small hardcoded EU-country list (DE, FR, NL, SE, NO, FI, IS, IE, ES, IT, AT, BE, LU, PL, CZ, …) so Region: "EU" matches any of them. Or expose `region_countries:` in the skillset for explicit control.
- **ApiAdapter pagination.** `ApiAdapter.FetchAsync` does a single GET. Add a `pagination:` block to `PortalConfig` (`offset_param`, `page_size`, `max_pages`) and loop with a safety cap.
- **Rate limiting.** `PortalConfig.RateLimitRps` is parsed but not honoured. Worth wiring once pagination lands.
- **CSV quoted-newline support.** `ManualAdapter.ReadCsvFile` uses `StreamReader.ReadLine()`, which splits inside quoted fields that span lines. Upgrade to a stream-based parser.
- **CancellationToken plumbing.** Adapters don't accept a CT yet — needs threading once the search handler is the orchestrator.
- **Playwright install path hardcoded.** `HtmlAdapter` and `ConfigVerifier` reference `bin/Debug/net10.0/playwright.ps1`. Resolve via `AppContext.BaseDirectory`.
- **`ApiAdapter.RenderTemplate` silently drops unknown template keys.** Warn or throw.
- **Remove unused `PortalConfig.BaseUrl`.** Parsed and stored, never read.

## Nice-to-haves

- Live smoke against `jobnet.dk` behind an env flag (off in CI).
- YAML frontmatter at the top of `top_jobs.md` for downstream tooling.
- Markdown table escaping for `|` in titles/companies.

## Bugs deferred

- `ScoreSeniority` adjacent case returns `(0.5, true)` — reasoning says "Seniority fits" for half-credit adjacent matches. Cleanest fix is a tri-state on `MatchReasoning`.
- `InferSeniority` looks only at the title, not the description.
