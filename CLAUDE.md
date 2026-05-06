# CLAUDE.md

Working notes for agents (Claude Code, sub-agents) operating in this repo.

## Repo shape (read this first)

```
docs/                  prd.md, requirements.md
src/                   ALL source, tests, configs, build infra
  Jobmatch/            class library — models, parsing, adapters, ranking, dedupe, output, verification
  Jobmatch.Gui/        Kestrel + React 19 SPA
  Jobmatch.Tests/      xUnit tests
  config/              committed example/default configs (skillset.example.md, portals.example.yml, ranking.yml)
  Directory.Build.props
  Directory.Packages.props
  Jobmatch.slnx
data/                  GITIGNORED — per-user state under data/<email>/
                       (typically a junction/symlink to a personal sync folder; never tracked)
  <email>/
    skillset.md, portals.yml, [ranking.yml override]
    raw/, imports/
    all_listings.json, ranked_listings.json, top_jobs.md
    examples/                 user-curated seed listings (liked / disliked archetypes)
    history/<run-id>.json
    marks.json
README.md              business-level intro
todo.md                ongoing/completed/backlog
global.json            SDK pin (.NET 10)
```

## Source of truth for product decisions

- **What the product is** → [`docs/prd.md`](docs/prd.md)
- **What the system must do** → [`docs/requirements.md`](docs/requirements.md) (one-line requirements with `R-NNN` IDs)
- **What's in flight** → [`todo.md`](todo.md)
- **How the architecture should look** → read `src/Jobmatch.Gui/Server/`. The pattern (Endpoints / Handlers / Models, centralised `Routes.cs`, SSE for long-running ops) is small enough to be self-evident from the code.

When changing behaviour, update the relevant requirement(s) before or with the code. When closing a task, update `todo.md`.

## Code conventions

- C# nullable reference types are on; treat warnings as errors. Keep them on.
- One concern per file. Models are immutable records. Validation lives at the GUI handler boundary — handlers in `Gui/Server/Handlers/*` call factory methods on records that throw `ConfigException` on bad input.
- Adapters throw on failure. The orchestrator (a `SearchService` or the GUI search handler) wraps each adapter in try/catch, logs `[WARN] portal=<name>`, and continues.
- No comments unless the *why* is non-obvious. No docstrings on simple methods. No "added for X" or "used by Y" notes — those rot.
- Tests live under `src/Jobmatch.Tests/` mirroring the source tree. xUnit. No live network calls in CI.

## Per-user data

- Every operation that reads or writes user state must resolve the path through `data/<email>/`. The active email comes from `git config user.email`, falling back to env var `JOBFINDER_USER`, falling back to a clear error. The GUI exposes the email switch as a setting.
- The committed `src/config/*.example.*` files are templates copied into `data/<email>/` on first use.
- `src/config/ranking.yml` is the default; if `data/<email>/ranking.yml` exists, it overrides.
- Never write user state outside `data/<email>/`. Never commit anything from `data/`.

## Entry point

- Single binary. The published assembly is `Jobmatch.Gui` and launching it starts an ephemeral Kestrel server, opens the default browser, and serves the React SPA. There is no separate CLI; headless operation is not part of v1.
- The `Jobmatch/` library is the single backbone. The GUI handlers are the only callers.
- The GUI layout: `Server/Endpoints/`, `Server/Handlers/`, `Server/Models/`, centralised `Routes.cs`, `/api/ping` heartbeat, `/api/shutdown`, SSE for long-running operations, Vite + React 19 + React Query.

## Things to avoid

- **No hard-coded personal context in code.** No keywords, locations, employers, or stacks bake into binaries. Everything personal is data.
- **No anti-bot bypassing.** Sites that block automation are supported only via the `manual` provider type.
- **No background daemons or schedulers.** This is an on-demand tool.
- **No telemetry or external state.** Everything is local.
- **No global state store / reducer pattern.** Jobfinder is stateless per call — sharing through the `Jobmatch/` library + DTO contracts is enough.
- **No re-introduction of a CLI without product approval.** The CLI was removed when the GUI became the contract; revisit only with explicit user direction.

## When in doubt

- Re-read [`docs/prd.md`](docs/prd.md) for principle.
- Re-read [`docs/requirements.md`](docs/requirements.md) for the contract.
- Read `src/Jobmatch.Gui/Server/` for the architecture pattern.
