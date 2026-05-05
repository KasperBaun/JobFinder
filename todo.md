# todo

Current status of work on `jobfinder`. Per-task specs live in [`docs/tasks/`](docs/tasks/).

## In progress

_(none — restructure landed; next milestone is T-001)_

## Known build break (pre-existing, not from the restructure)

`src/Directory.Packages.props` bumps `Spectre.Console.Cli` from 0.49.1 → 0.55.0 (modification was already in the working tree before the restructure landed). 0.55's `AsyncCommand<T>.ExecuteAsync` adds a `CancellationToken` parameter to the override signature, so `Listings/Skillset/VerifyCommand` no longer compile.

Two ways out, pick when ready:
- Revert `Spectre.Console.Cli` (and `Spectre.Console`, `Spectre.Console.Testing`) back to 0.49.1.
- Update each command's `ExecuteAsync` to take the new 3rd parameter and thread it through (also fixes the `CancellationToken plumbing` item below — two birds).

## Backlog (next up)

| ID | Task | Spec |
|---|---|---|
| T-001 | Adopt per-user data path (`data/<email>/`) for all reads/writes | [`docs/tasks/T-001-per-user-data-path.md`](docs/tasks/T-001-per-user-data-path.md) |
| T-002 | GUI scaffolding — Kestrel + Vite/React, mwt server pattern | [`docs/tasks/T-002-gui-scaffolding.md`](docs/tasks/T-002-gui-scaffolding.md) |
| T-003 | GUI feature pages — providers, skillset, search (SSE), history, marks | [`docs/tasks/T-003-gui-feature-pages.md`](docs/tasks/T-003-gui-feature-pages.md) |

## Completed (recent)

- **Repo restructure** — `docs/`, `src/` (with tests + configs), `data/<email>/`, root `README.md`, `CLAUDE.md`, `todo.md`. Build infra moved under `src/`. Existing user state migrated to `data/tappah2510@gmail.com/`.
- **`docs/prd.md`** — rewritten as actor + vision PRD.
- **`docs/requirements.md`** — new one-line requirement list (`R-001`..`R-084`).
- **`docs/mwt-tool-analysis.md`** — analysis of the `mwt` GUI/Server architecture pattern jobfinder will adopt.

(Anything older than the restructure lives in git history.)

## Pending engine improvements (carried over from previous todo)

These pre-date the restructure. Still valid; will fold into the GUI work where they touch the same code paths.

- **Country → region mapping for cross-EU remotes.** A listing in "Germany" for an EU-region user falls to else tier (0.1) because the matcher only recognises "EU/Europe/EMEA" synonyms, not member states. Add a small hardcoded EU-country list (DE, FR, NL, SE, NO, FI, IS, IE, ES, IT, AT, BE, LU, PL, CZ, …) so Region: "EU" matches any of them. Or expose `region_countries:` in the skillset for explicit control.
- **ApiAdapter pagination.** `ApiAdapter.FetchAsync` does a single GET. Add a `pagination:` block to `PortalConfig` (`offset_param`, `page_size`, `max_pages`) and loop with a safety cap.
- **Rate limiting.** `PortalConfig.RateLimitRps` is parsed but not honoured. Worth wiring once pagination lands.
- **CSV quoted-newline support.** `ManualAdapter.ReadCsvFile` uses `StreamReader.ReadLine()`, which splits inside quoted fields that span lines. Upgrade to a stream-based parser.
- **CancellationToken plumbing.** `ListingsCommand.ExecuteAsync` doesn't pass a CT to adapters — Ctrl+C only bails after the current adapter finishes.
- **Playwright install path hardcoded.** `HtmlAdapter` and `ConfigVerifier` reference `bin/Debug/net10.0/playwright.ps1`. Resolve via `AppContext.BaseDirectory`.
- **`ApiAdapter.RenderTemplate` silently drops unknown template keys.** Warn or throw.
- **Remove unused `PortalConfig.BaseUrl`.** Parsed and stored, never read.
- **Additional `--explain` test on an INCLUDED listing.**

## Nice-to-haves

- Live smoke against `jobnet.dk` behind an env flag (off in CI).
- YAML frontmatter at the top of `top_jobs.md` for downstream tooling.
- Markdown table escaping for `|` in titles/companies.
- Split `CliApp.Create(console?)` test seam out of `CliApp`.

## Bugs deferred

- `ScoreSeniority` adjacent case returns `(0.5, true)` — reasoning says "Seniority fits" for half-credit adjacent matches. Cleanest fix is a tri-state on `MatchReasoning`.
- `InferSeniority` looks only at the title, not the description.
