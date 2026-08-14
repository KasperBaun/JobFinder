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
package.json                         root npm wrapper — npm workspaces root (src/frontend, src/desktop,
                                     src/tests/playwright) plus convenience scripts around dotnet + npm
                                     (build/dev/test/package/tool). One `npm install` at the root installs
                                     every workspace; there is a single `package-lock.json`, at the root.
publish/                             GITIGNORED — self-contained win-x64 publish output
pkg/                                 GITIGNORED — local NuGet tool package (npm run package)
.claude/skills/                      project skills — dotnet-backend-standards owns the backend conventions (replaced src/backend/rules/), plus frontend-standards, developer-documentation, business-analysis, verify
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
- **How the backend should look** → the `dotnet-backend-standards` skill ([`.claude/skills/dotnet-backend-standards/`](.claude/skills/dotnet-backend-standards/SKILL.md) — SKILL.md plus the full rule set under its `reference/`): Endpoint → Handler → Service layering, HandlerBase + ExecuteAsync, IEndpointRegistration, typed Routes, custom exceptions, module pattern, file-size limits, coding conventions. Then read `src/backend/Jobmatch.Api/` to see the pattern applied. The structural/quality rules are the standard here; a few infra rules are deliberately excepted — see **Backend rules: adopted vs. exceptions** below.

When changing behaviour, update the relevant requirement(s) before or with the code. When closing a task, drop it from `todo.md` and record the result as **one lean line** in `CHANGELOG.md` (full detail belongs in the commit) — keep `todo.md` forward-looking (backlog + in-progress only), never a prose changelog.

## Backend rules: adopted vs. exceptions

The backend conventions live in the `dotnet-backend-standards` skill ([`.claude/skills/dotnet-backend-standards/`](.claude/skills/dotnet-backend-standards/SKILL.md)), which replaced the former `src/backend/rules/` tree — do not recreate that folder. The skill's `reference/` files were written for a multi-tenant SaaS (JWT auth, EF Core + SQL Server, GUID IDs); jobfinder is local, single-user, file-based, no-auth, and the skill's *"Jobfinder deviations from the generic rules"* table enumerates every carve-out. The deviations are codified design decisions, not violations to "fix" — the headline ones:

- **No auth / no `.RequirePermission()`.** Deferred; may be added later. (See "No auth" under *Things to avoid*.)
- **No EF Core / migrations.** State is JSON files under `data/<email>/`.
- **SQLite, not SQL Server**, for Hangfire storage (`data/<email>/hangfire.db`).
- **String timestamp run-ids, not GUID primary keys** (id == the history run id).
- **Hangfire dashboard local-only / unsecured by design** (no auth provider).
- **Retry policy (intentional):** the search job uses `[AutomaticRetry(Attempts = 1)]`, not the rule's default of 3. A full re-run is expensive, and per-provider failures are already handled gracefully inside the `SearchService` pipeline (each adapter wrapped in try/catch, logged, skipped). Do **not** "fix" this back to 3.

Everything else in the skill's `reference/` applies without carve-out: Endpoint → Handler → Service layering, `HandlerBase` + `ExecuteAsync`, `IEndpointRegistration`, typed `Routes.*`, centralised OpenAPI metadata, custom exceptions, the module pattern, the 300-line file / 50-line method limits and partial-class refactoring strategy, and the coding + testing conventions.

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
- The Electron desktop shell (`src/desktop/`, tracked TypeScript source) is the second front-end and **the** Windows installer going forward (`npm run package:win` → electron-builder NSIS installer, artifact under `src/desktop/release/`; also built by CI `release.yml`). It spawns that same `Jobmatch.Host.exe` on an ephemeral loopback port (`JOBFINDER_PORT` + `JOBFINDER_NO_BROWSER=1`, `windowsHide`) and renders the SPA in a native `BrowserWindow` (single-instance lock, graceful backend shutdown, startup-error window, remembered window size/position). Two settings there exist because of npm workspaces and must not be "tidied": `electron` is pinned to an exact version (electron-builder resolves it from `src/desktop/node_modules`, which hoisting empties, so a range makes the build fail), and `npmRebuild: false` in `electron-builder.yml` stops its app-dir `npm install --omit=dev` from pruning the whole root tree mid-build. Electron 43 needs Node ≥ 22.12 — that is the floor for local builds and for `release.yml`'s `setup-node`.
- There is no separate CLI; headless operation is not part of v1.
- The `Jobmatch/` library is the single backbone (services, ranking, parsing, adapters). The `Jobmatch.Api` project owns the HTTP layer. `Jobmatch.Host` is the deployment-time composition root.
- API layout: `src/backend/Jobmatch.Api/Endpoints/`, `Handlers/`, `Models/`, `Infrastructure/` (HandlerBase, IEndpointRegistration), centralised `Routes.cs` with `ApiConstants.RouteBase` prefix, `/api/system/ping` heartbeat, `/api/system/shutdown` (host-only), SSE for long-running operations, Vite + React 19 + React Query.

## Releasing (dev → main)

`main` is the release branch: **every push to it builds and publishes installers** via
`.github/workflows/release.yml`. Feature branches land in `dev`; `dev` reaches `main` only
through a pull request, **squash-merged** (`<PR title> (#N)`). Two GitHub rulesets enforce
this and are not to be worked around — ask before touching either:

- **`PR-main`** on the default branch — blocks deletion and non-fast-forward pushes, and
  requires a PR with squash as the only allowed merge method. Direct pushes to `main` fail
  with "push declined due to repository rule violations".
- **`protect-dev`** on `refs/heads/dev` — blocks deletion and non-fast-forward pushes.
  Ordinary commits can still be pushed straight to `dev`. It exists because a merge once
  deleted `dev`, which auto-closed the open PR that targeted it.

1. **Verify functional first** — `dotnet test src/Jobmatch.slnx -c Release`
   (with `JOBFINDER_USER` set; the runner has no `git config user.email`),
   `npm run test -w jobfinder-gui`, and `npm run build -w jobfinder-gui`
   (`tsc -b` is what catches a missing Danish catalog key). CI reruns all three on
   Windows *and* Linux, so a red suite here is a failed release there.
2. **Housekeeping** — `todo.md` forward-looking only (closed items dropped, "In progress"
   empty), one lean line per change in `CHANGELOG.md` under `## Recent` (the file has no
   per-version headings — don't add any), and an `R-NNN` in `docs/requirements.md` for
   every behaviour change. Task plan docs under `docs/tasks/` stay after shipping — they
   are design rationale, like `T-007/`.
3. **Version bump commit** on `dev` — `chore(release): bump to X.Y`, touching exactly four
   files: `package.json`, `package-lock.json` (CI runs `npm ci` at the root before
   `npm version`, so a stale lock fails the build), `src/desktop/package.json`, and the
   `VERSION:` prefix in `.github/workflows/release.yml`. Run
   `npm version X.Y.0 --no-git-tag-version` then
   `npm version X.Y.0 -w jobfinder-desktop --no-git-tag-version` — both write the single
   root lock, so packages and lock stay in sync. Only the minor is bumped by hand — the
   patch is `${{ github.run_number }}`.
4. **PR `dev` → `main`**, titled like the release (`Release 0.4 — <headline>`), squash-merged
   — the ruleset allows nothing else. The PR title becomes the commit subject on `main`, so
   it is the release's permanent label.
5. **The merge is the release.** CI tests both platforms, publishes the self-contained
   backend, builds the NSIS `.exe` and the `.deb`, then wipes and re-uploads the assets on
   the rolling **`latest`** prerelease tag. There are no per-version git tags, and `latest`
   keeps its original creation date while its assets are replaced — check
   `gh run list --workflow=release.yml`, not the release date, to confirm a build shipped.
6. **Merge `main` back into `dev`** afterwards. A squash merge rewrites the release as one
   new commit that is not in `dev`'s history, so the two branches diverge by construction —
   `git checkout dev && git merge origin/main` reconciles them (the trees are already
   identical, so it is an empty merge). Resetting `dev` onto `main` instead is not an
   option: `protect-dev` blocks the non-fast-forward push.

## Localization

The GUI ships English and Danish. Catalogs live in `src/frontend/src/i18n/en/` and
`src/frontend/src/i18n/da/`, one module per feature namespace; `useT('namespace')`
returns the namespace object, so call sites are property accesses, not string keys.

- **Both locales land in the same change.** `da/index.ts` is annotated with the English
  catalog's type, so a missing key, an extra key or a mismatched interpolation signature
  is a `tsc` error — and `tsc -b` runs in CI via the release publish. Never add a string
  to one catalog only.
- **No user-facing string literals in components** — including `aria-label`, `title`,
  `placeholder` and `confirm()` text. Values keyed by a domain enum (statuses, phases,
  drop reasons) belong in the catalog as `satisfies Record<TheEnum, string>`.
- **Dates, numbers and sorting go through `i18n/format.ts`** (`n`, `dec`, `collator`,
  `relativeTimeFormat`, `dateTimeFormat`) or `utils/time.ts`, never bare `toFixed`,
  `toLocaleString` or `localeCompare` — Danish uses `0,82` / `1.234` and sorts æ/ø/å
  after z.
- **The language lives in `bootstrap.json`** (`BootstrapConfig.Language`, set via the
  setup request or `PUT /api/settings/language`), with a `localStorage` copy used only as
  a boot hint so reloads don't flash English.
- **Backend prose travels as `key + args`, never as finished sentences.** Timeline entries
  (`Jobs/JobSearch.cs`, `Jobmatch.Api/Jobs/SearchJob.Events.cs`) and match rationale
  (`Ranking/Ranker.Notes.cs`) emit a stable key plus the values it interpolates; the
  frontend's `server` namespace owns the wording. Each also keeps its English string
  (`Message`, `Notes`) — that is what logs, `top-jobs.md` and runs recorded before the keys
  show. Drop reasons (`Search/SearchService.Ranking.cs`) still emit key + args + English
  context into run history, but nothing renders them since the removed view was retired.
  **Keys are persisted in run history, so they are additive only: never rename or repurpose
  one.** Add a key to both `en/server.ts` and `da/server.ts` in the same change.
- **The English wording exists twice on purpose** — `Ranker.Notes.cs` renders `top-jobs.md`
  and the persisted prose, `i18n/en/server.ts` renders the UI. They cannot both be dropped,
  so `src/tests/fixtures/reasoning-en.json` pins them: a C# test and a Vitest test assert
  the *same* entries, and changing one implementation alone fails a test. Edit the fixture
  and both sides together.
- **Still English:** API error messages, `top-jobs.md`, the verification report, and
  LLM-generated text.
- Brand names (`utils/platform.ts` ATS labels) and wire formats (the longlist URL hash)
  are intentionally untranslated.

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
  start a server or create a db. The skill's `reference/infrastructure/background-jobs.md` conventions apply (storage backend and dashboard auth excepted — see above).

## Product & ranking constraints (don't regress these)

Durable decisions that outlive any single task — migrated here from earlier handoff/plan docs so they survive:

- **Strict primary stack.** Don't loosen `require_primary_stack_hit`: .NET/C#/Azure, TypeScript/React, SQL. No Rust/Python/Go roles, however strong the employer or seniority signal.
- **Ranking success metric.** The only measure that counts is "would the user take one of the top-10 jobs?", judged against the curated `examples/` seed listings — NOT test counts, top-score deltas, or rule coverage.
- **No external-service dependency.** The AI judge runs in-process via LlamaSharp (Gemma GGUF), fully offline. No Docker, no Ollama, no network at rank time — this is why LlamaSharp is the default, not just a convenience.

## When in doubt

- Re-read [`docs/prd.md`](docs/prd.md) for principle.
- Re-read [`docs/requirements.md`](docs/requirements.md) for the contract.
- Re-read the `dotnet-backend-standards` skill ([`.claude/skills/dotnet-backend-standards/`](.claude/skills/dotnet-backend-standards/SKILL.md)) for backend conventions.
- Read `src/backend/Jobmatch.Api/` for the conventions applied to actual code.
