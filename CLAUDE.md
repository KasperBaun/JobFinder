# CLAUDE.md

Working notes for agents (Claude Code, sub-agents) operating in this repo.

## Repo shape (read this first)

```
docs/                                prd.md, requirements.md
src/                                 ALL source, tests, configs, build infra
  backend/
    Jobmatch/                        class library — models, parsing, adapters, ranking, dedupe, output, verification, services
    Jobmatch.Api/                    Minimal API server (runnable). Endpoints/, Handlers/, Models/, Routes.cs, Infrastructure/
    config/                          committed example/default configs (skillset.example.md, ranking.yml)
    rules/                           backend conventions docs (api/, conventions/, data-access/, infrastructure/, security/, testing/)
  frontend/                          React 19 + Vite app (runnable independently against Jobmatch.Api)
  infrastructure/
    Jobmatch.Host/                   bundle (runnable + .NET tool). Ephemeral Kestrel + browser-open + serves bundled SPA + jobfinder tool packaging
  tests/
    Jobmatch.Tests/                  xUnit
    playwright/                      Playwright e2e (bootstrap; specs added incrementally)
  Directory.Build.props
  Directory.Packages.props
  Jobmatch.slnx
data/                                GITIGNORED — per-user state under data/<email>/
                                     (typically a junction/symlink to a personal sync folder; never tracked)
  <email>/
    skillset.md, portals.yml, [ranking.yml override]
    raw/, imports/
    all_listings.json, ranked_listings.json, top_jobs.md
    examples/                        user-curated seed listings (liked / disliked archetypes)
    history/<run-id>.json
    marks.json
README.md                            business-level intro
todo.md                              ongoing/completed/backlog
global.json                          SDK pin (.NET 10)
```

## Source of truth for product decisions

- **What the product is** → [`docs/prd.md`](docs/prd.md)
- **What the system must do** → [`docs/requirements.md`](docs/requirements.md) (one-line requirements with `R-NNN` IDs)
- **What's in flight** → [`todo.md`](todo.md)
- **Why each DK portal got the verdict it did** → [`docs/tasks/T-007/`](docs/tasks/T-007/) — per-portal evaluation worksheets (api / rss / html / manual / dead) + the playbook for evaluating a new one. Reference data, not a task spec — keep when adding or reconsidering portals.
- **How the backend should look** → read `src/backend/rules/` for the conventions (Endpoint → Handler → Service layering, HandlerBase + ExecuteAsync, IEndpointRegistration, typed Routes, custom exceptions, module pattern, file-size limits, coding conventions). Then read `src/backend/Jobmatch.Api/` to see the pattern applied. Auth/EF/Hangfire rules in `src/backend/rules/` do not apply — jobfinder is local single-user file-based.

When changing behaviour, update the relevant requirement(s) before or with the code. When closing a task, update `todo.md`.

## Code conventions

- C# nullable reference types are on; treat warnings as errors. Keep them on.
- One concern per file. Models are immutable records. Validation lives in services — services throw `ConfigException` / `InvalidRequestException` / `NotFoundException` and `HandlerBase.ExecuteAsync` translates to HTTP responses.
- Adapters throw on failure. The `SearchService` orchestrator wraps each adapter in try/catch, logs structured warnings, and continues.
- No comments unless the *why* is non-obvious. No docstrings on simple methods. No "added for X" or "used by Y" notes — those rot.
- Tests live under `src/tests/Jobmatch.Tests/` mirroring the source tree. xUnit. No live network calls in CI.

## Per-user data

- Every operation that reads or writes user state must resolve the path through `data/<email>/`. The active email comes from `git config user.email`, falling back to env var `JOBFINDER_USER`, falling back to a clear error. The GUI exposes the email switch as a setting.
- The committed `src/backend/config/*.example.*` files are templates copied into `data/<email>/` on first use.
- `src/backend/config/ranking.yml` is the default; if `data/<email>/ranking.yml` exists, it overrides.
- Never write user state outside `data/<email>/`. Never commit anything from `data/`.

## Entry point

- Single binary. The published `jobfinder` .NET tool is `Jobmatch.Host`; launching it starts an ephemeral Kestrel server, opens the default browser, and serves the bundled React SPA from `gui/`. There is no separate CLI; headless operation is not part of v1.
- The `Jobmatch/` library is the single backbone (services, ranking, parsing, adapters). The `Jobmatch.Api` project owns the HTTP layer. `Jobmatch.Host` is the deployment-time composition root.
- API layout: `src/backend/Jobmatch.Api/Endpoints/`, `Handlers/`, `Models/`, `Infrastructure/` (HandlerBase, IEndpointRegistration), centralised `Routes.cs` with `ApiConstants.RouteBase` prefix, `/api/system/ping` heartbeat, `/api/system/shutdown` (host-only), SSE for long-running operations, Vite + React 19 + React Query.

## Things to avoid

- **No hard-coded personal context in code.** No keywords, locations, employers, or stacks bake into binaries. Everything personal is data.
- **No anti-bot bypassing.** Sites that block automation are supported only via the `manual` provider type.
- **No background daemons or schedulers.** This is an on-demand tool.
- **No telemetry or external state.** Everything is local.
- **No global state store / reducer pattern.** Jobfinder is stateless per call — sharing through the `Jobmatch/` library + DTO contracts is enough.
- **No re-introduction of a CLI without product approval.** The CLI was removed when the GUI became the contract; revisit only with explicit user direction.
- **No DB / auth / Hangfire.** The rules in `src/backend/rules/` reference these; they do not apply to jobfinder. State is files under `data/<email>/`; there is no auth.

## When in doubt

- Re-read [`docs/prd.md`](docs/prd.md) for principle.
- Re-read [`docs/requirements.md`](docs/requirements.md) for the contract.
- Re-read [`src/backend/rules/`](src/backend/rules/) for backend conventions.
- Read `src/backend/Jobmatch.Api/` for the conventions applied to actual code.
