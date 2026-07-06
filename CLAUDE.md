# CLAUDE.md

Working notes for agents (Claude Code, sub-agents) operating in this repo.

## Repo shape (read this first)

```
docs/                                prd.md, requirements.md, tasks/T-007/ (portal reference), screenshots/
src/                                 ALL source, tests, configs, build infra
  backend/
    Jobmatch/                        class library — models, parsing, adapters, ranking, dedupe, output, verification, services
    Jobmatch.Api/                    Minimal API server (runnable). Endpoints/, Handlers/, Models/, Routes.cs, Infrastructure/
    config/                          committed example/default configs (skillset.example.md, ranking.yml)
    rules/                           backend conventions docs (api/, conventions/, data-access/, infrastructure/, security/, testing/)
  frontend/                          React 19 + Vite app (runnable independently against Jobmatch.Api)
  desktop/                           Electron shell (TS) — spawns Jobmatch.Host.exe on a loopback port, renders the SPA in a BrowserWindow (see "Entry point"). src/ is tracked; dist/, node_modules/, release/ are gitignored build output.
  infrastructure/
    Jobmatch.Host/                   bundle (runnable + .NET tool). Ephemeral Kestrel + browser-open + serves bundled SPA + jobfinder tool packaging
  scripts/                           Node build/dev wrappers (dev.mjs, package*.mjs, *-tool.mjs, clean/refresh) — driven by root package.json.
  tests/
    Jobmatch.Tests/                  xUnit
    playwright/                      Playwright e2e (bootstrap; specs added incrementally)
  Directory.Build.props
  Directory.Packages.props
  Jobmatch.slnx
data/                                GITIGNORED — per-user state under data/<email>/ (may be a junction/symlink to a personal sync folder; never tracked). Live dir can be redirected on first run — see Per-user data.
  <email>/
    skillset.md, portals.yml, [ranking.yml override]
    raw/, imports/
    all-listings.json, ranked-listings.json, top-jobs.md
    examples/                        user-curated seed listings (liked / disliked archetypes)
    history/<run-id>.json, jobsearch/<id>.json, hangfire.db
    marks.json
package.json                         root npm wrapper — convenience scripts around dotnet + npm (build/dev/test/package/tool)
publish/                             GITIGNORED — self-contained win-x64 publish output
pkg/                                 GITIGNORED — local NuGet tool package (npm run package)
.github/workflows/                   CI — release.yml builds the Windows installer on push to main
README.md                            business-level intro
todo.md                              backlog + in-progress (forward-looking only)
CHANGELOG.md                         shipped work — one lean line per change
```

The SDK is pinned only by `<TargetFramework>net10.0</TargetFramework>` in `src/Directory.Build.props` (no `global.json`).

## Source of truth for product decisions

- **What the product is** → [`docs/prd.md`](docs/prd.md)
- **What the system must do** → [`docs/requirements.md`](docs/requirements.md) (one-line requirements with `R-NNN` IDs)
- **What's in flight** → [`todo.md`](todo.md) (backlog + in-progress only)
- **What's shipped** → [`CHANGELOG.md`](CHANGELOG.md)
- **Why each DK portal got the verdict it did** → [`docs/tasks/T-007/`](docs/tasks/T-007/) — per-portal evaluation worksheets (api / rss / html / manual / dead) + the playbook for evaluating a new one. Reference data, not a task spec — keep when adding or reconsidering portals.
- **How the backend should look** → read `src/backend/rules/` for the conventions (Endpoint → Handler → Service layering, HandlerBase + ExecuteAsync, IEndpointRegistration, typed Routes, custom exceptions, module pattern, file-size limits, coding conventions). Then read `src/backend/Jobmatch.Api/` to see the pattern applied. The structural/quality rules are the standard here; a few infra rules are deliberately excepted — see **Backend rules: adopted vs. exceptions** below.

When changing behaviour, update the relevant requirement(s) before or with the code. When closing a task, drop it from `todo.md` and record the result as **one lean line** in `CHANGELOG.md` (full detail belongs in the commit) — keep `todo.md` forward-looking (backlog + in-progress only), never a prose changelog.

## Backend rules: adopted vs. exceptions

`src/backend/rules/` was written for a multi-tenant SaaS (JWT auth, EF Core + SQL Server, GUID IDs). Jobfinder is local, single-user, file-based, no-auth. The split below is intentional: the **adopted** rules are the standard and bind all backend work; the **exceptions** are codified design decisions, not violations to "fix."

**Adopted — these apply, no carve-out:**

- Endpoint → Handler → Service layering; `HandlerBase` + `ExecuteAsync`; `IEndpointRegistration`; typed `Routes.*`; centralised OpenAPI metadata.
- Custom exceptions (`ConfigException` / `InvalidRequestException` / `NotFoundException`) translated to HTTP by `ExecuteAsync`; the module pattern.
- One concern per file; file/method size limits (300-line file / 50-line method hard limits); the partial-class refactoring strategy when a file outgrows the limit.
- Coding conventions: collection expressions `[]`, `.Count > 0` over `.Any()`, primary constructors, enums for domain values, immutable records.
- Testing conventions: xUnit, FluentAssertions, tests mirror the source tree, same size limits.

**Deliberate exceptions — intentional for a local/single-user/file-based tool:**

- **No auth / no `.RequirePermission()`.** Deferred; may be added later. (See "No auth" under *Things to avoid*.)
- **No EF Core / migrations.** State is JSON files under `data/<email>/`.
- **SQLite, not SQL Server**, for Hangfire storage (`data/<email>/hangfire.db`).
- **String timestamp run-ids, not GUID primary keys** (id == the history run id).
- **Hangfire dashboard local-only / unsecured by design** (no auth provider).

`rules/infrastructure/background-jobs.md` **applies** to the sanctioned search job (see *Background search jobs*) **except** the storage backend (SQLite, not SQL Server) and dashboard auth.

- **Retry policy (intentional):** the search job uses `[AutomaticRetry(Attempts = 1)]`, not the rule's default of 3. A full re-run is expensive, and per-provider failures are already handled gracefully inside the `SearchService` pipeline (each adapter wrapped in try/catch, logged, skipped). Do **not** "fix" this back to 3.

## Code conventions

- C# nullable reference types are on; treat warnings as errors. Keep them on.
- One concern per file. Models are immutable records. Validation lives in services — services throw `ConfigException` / `InvalidRequestException` / `NotFoundException` and `HandlerBase.ExecuteAsync` translates to HTTP responses.
- Adapters throw on failure. The `SearchService` orchestrator wraps each adapter in try/catch, logs structured warnings, and continues.
- No comments unless the *why* is non-obvious. No docstrings on simple methods. No "added for X" or "used by Y" notes — those rot.
- Tests live under `src/tests/Jobmatch.Tests/` mirroring the source tree. xUnit. No live network calls in CI.

## Per-user data

- Every operation that reads or writes user state must resolve the path through `data/<email>/`. The active email comes from `git config user.email`, falling back to env var `JOBFINDER_USER`, falling back to a clear error. The GUI exposes the email switch as a setting.
- On first launch the GUI asks the user to confirm where data lives; the choice (email + absolute data dir) is persisted to `%APPDATA%/jobfinder/bootstrap.json` (`BootstrapStore`) and used verbatim on later runs — for both state (`UserContextProvider`) and the host log (`LogLocation.ResolveRootDir`, `Jobmatch.Host/Program.cs`). So once bootstrap is set the live data dir sits wherever the user chose (e.g. `%LOCALAPPDATA%/jobfinder/`), even inside a git checkout, and the repo-root `data/` is not written to — any `data/` left in the repo is a stale skeleton from earlier runs and safe to delete.
- The committed `src/backend/config/*.example.*` files are templates copied into `data/<email>/` on first use.
- `src/backend/config/ranking.yml` is the default; if `data/<email>/ranking.yml` exists, it overrides.
- Never write user state outside `data/<email>/`. Never commit anything from `data/`.
- When run outside a git repo (no `.git` anchor up the chain), `data/<email>/` is created under
  `%LOCALAPPDATA%/jobfinder/` (Windows) / `~/.local/share/jobfinder/` (Unix) instead of the cwd.

## Entry point

- One backend, two front-end shells. The backbone is the self-contained `Jobmatch.Host`: launching it starts an ephemeral Kestrel server, opens the default browser, and serves the bundled React SPA from `gui/`. This browser experience ships as the `jobfinder` .NET tool (`npm run package` / `install:tool`) and runs via `npm run dev` / `dev:bundled`. It is being retired in favour of the desktop app but stays functional; it no longer has its own Windows installer.
- The Electron desktop shell (`src/desktop/`, tracked TypeScript source) is the second front-end and **the** Windows installer going forward (`npm run package:win` → electron-builder NSIS installer, artifact under `src/desktop/release/`; also built by CI `release.yml`). It spawns that same `Jobmatch.Host.exe` on an ephemeral loopback port (`JOBFINDER_PORT` + `JOBFINDER_NO_BROWSER=1`, `windowsHide`) and renders the SPA in a native `BrowserWindow` (single-instance lock, graceful backend shutdown, startup-error window, remembered window size/position).
- There is no separate CLI; headless operation is not part of v1.
- The `Jobmatch/` library is the single backbone (services, ranking, parsing, adapters). The `Jobmatch.Api` project owns the HTTP layer. `Jobmatch.Host` is the deployment-time composition root.
- API layout: `src/backend/Jobmatch.Api/Endpoints/`, `Handlers/`, `Models/`, `Infrastructure/` (HandlerBase, IEndpointRegistration), centralised `Routes.cs` with `ApiConstants.RouteBase` prefix, `/api/system/ping` heartbeat, `/api/system/shutdown` (host-only), SSE for long-running operations, Vite + React 19 + React Query.

## Things to avoid

- **No hard-coded personal context in code.** No keywords, locations, employers, or stacks bake into binaries. Everything personal is data.
- **No anti-bot bypassing.** Sites that block automation are supported only via the `manual` provider type.
- **No background *schedulers* / recurring daemons.** Still no cron-like or always-on background work. The one sanctioned background *job* is the user-initiated search (see below) — transient, scoped to a single run, not recurring.
- **No telemetry or external state.** Everything is local.
- **No global state store / reducer pattern.** Jobfinder is stateless per call. Exception: the frontend `SearchRunContext` and the server `JobSearch` store track exactly one in-flight search's lifecycle — not general app state.
- **No re-introduction of a CLI without product approval.** The CLI was removed when the GUI became the contract; revisit only with explicit user direction.
- **No auth.** State is files under `data/<email>/`; there is no auth. (Hangfire is used — see below — but its dashboard is local-only, no auth provider.)

## Background search jobs (the one sanctioned exception)

A search runs as a **Hangfire background job** (durable SQLite storage at `data/<email>/hangfire.db`),
decoupled from the HTTP request, so it survives navigation, reload, and host restart (R-036/R-037/R-038/R-055).
Do **not** "fix" this back to a synchronous in-request run.

- Domain model: `Jobmatch/Jobs/JobSearch.cs` (immutable record + state machine), persisted per-run via
  `JobSearchStore` under `data/<email>/jobsearch/<id>.json`. Id == the history run id.
- Execution: `Jobmatch.Api/Jobs/SearchJob.cs` (the Hangfire job) drives the `SearchService` pipeline,
  projects progress onto the `JobSearch` + timeline, and publishes snapshots to `JobSearchBus` for SSE.
- API: `POST /api/search` enqueues and returns `{ id }`; `GET /api/search/{id}/stream` is the SSE feed;
  `/api/search/active` for reconnect; `POST /api/search/{id}/cancel`.
- DI gate: `AddJobmatchApi(enableBackgroundJobs)` — false in the "Testing" environment so tests don't
  start a server or create a db. The `rules/infrastructure/background-jobs.md` conventions now apply.

## Product & ranking constraints (don't regress these)

Durable decisions that outlive any single task — migrated here from earlier handoff/plan docs so they survive:

- **Strict primary stack.** Don't loosen `require_primary_stack_hit`: .NET/C#/Azure, TypeScript/React, SQL. No Rust/Python/Go roles, however strong the employer or seniority signal.
- **Ranking success metric.** The only measure that counts is "would the user take one of the top-10 jobs?", judged against the curated `examples/` seed listings — NOT test counts, top-score deltas, or rule coverage.
- **No external-service dependency.** The AI judge runs in-process via LlamaSharp (Gemma GGUF), fully offline. No Docker, no Ollama, no network at rank time — this is why LlamaSharp is the default, not just a convenience.

## When in doubt

- Re-read [`docs/prd.md`](docs/prd.md) for principle.
- Re-read [`docs/requirements.md`](docs/requirements.md) for the contract.
- Re-read [`src/backend/rules/`](src/backend/rules/) for backend conventions.
- Read `src/backend/Jobmatch.Api/` for the conventions applied to actual code.
