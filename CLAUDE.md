# CLAUDE.md

Working notes for agents (Claude Code, sub-agents) operating in this repo.

## Repo shape (read this first)

```
docs/                  prd.md, requirements.md, mwt-tool-analysis.md, tasks/, implementation-plan.md
src/                   ALL source, tests, configs, build infra
  Jobmatch/            class library — models, parsing, adapters, ranking, dedupe, output, verification
  Jobmatch.Cli/        System.CommandLine entry point (CLI mode)
  Jobmatch.Gui/        (planned) Kestrel + React SPA — see docs/mwt-tool-analysis.md
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
    history/<run-id>.json     (planned)
    marks.json                (planned)
README.md              business-level intro
todo.md                ongoing/completed/backlog — references docs/tasks/
global.json            SDK pin (.NET 10)
.claude/commands/      Claude Code slash commands (must stay at root for discovery)
```

## Source of truth for product decisions

- **What the product is** → [`docs/prd.md`](docs/prd.md)
- **What the system must do** → [`docs/requirements.md`](docs/requirements.md) (one-line requirements with `R-NNN` IDs)
- **How the architecture should look** → [`docs/mwt-tool-analysis.md`](docs/mwt-tool-analysis.md) (mirrors the `mwt` GUI/Server pattern)
- **What's in flight** → [`todo.md`](todo.md) and [`docs/tasks/`](docs/tasks/)

When changing behaviour, update the relevant requirement(s) before or with the code. When closing a task, update `todo.md` *and* the matching task spec.

## Code conventions

- C# nullable reference types are on; treat warnings as errors. Keep them on.
- One concern per file. Models are immutable records. Validation lives at boundaries — entry-point handlers (`Cli/Commands/*`, `Gui/Server/Handlers/*`) call factory methods on records that throw `ConfigException` on bad input.
- Adapters throw on failure. The orchestrator (currently `ListingsCommand`, later `SearchHandler`) wraps each adapter in try/catch, logs `[WARN] portal=<name>`, and continues.
- No comments unless the *why* is non-obvious. No docstrings on simple methods. No "added for X" or "used by Y" notes — those rot.
- Tests live under `src/Jobmatch.Tests/` mirroring the source tree. xUnit. No live network calls in CI.

## Per-user data

- Every operation that reads or writes user state must resolve the path through `data/<email>/`. The active email comes from `--user <email>`, falling back to `git config user.email`, falling back to a prompt on first run.
- The committed `src/config/*.example.*` files are templates copied into `data/<email>/` on first use.
- `src/config/ranking.yml` is the default; if `data/<email>/ranking.yml` exists, it overrides.
- Never write user state outside `data/<email>/`. Never commit anything from `data/`.

## Modes & entry points

- `Program.cs` is a mode router: no args → GUI; otherwise CLI command tree (System.CommandLine, mirroring `mwt`'s pattern).
- The `Jobmatch/` library is the single backbone. CLI and GUI handlers both call into it. Neither references the other.
- The GUI (planned) follows `mwt`'s layout exactly: `Server/Endpoints/`, `Server/Handlers/`, `Server/Models/`, centralised `Routes.cs`, `/api/ping` heartbeat, `/api/shutdown`, SSE for long-running operations, Vite + React 19 + React Query.

## Slash commands

Three Claude Code slash commands in `.claude/commands/` shell out to the CLI. Update both the slash command and the CLI subcommand together — don't drift them.

| Slash command | Purpose |
|---|---|
| `/generate-skillset` | Author or refresh `data/<email>/skillset.md` |
| `/generate-job-listings` | Run a search, write top-N |
| `/verify-config` | Validate active configs and provider connectivity |

## Things to avoid

- **No hard-coded personal context in code.** No keywords, locations, employers, or stacks bake into binaries. Everything personal is data.
- **No anti-bot bypassing.** Sites that block automation are supported only via the `manual` provider type.
- **No background daemons or schedulers.** This is an on-demand tool.
- **No telemetry or external state.** Everything is local.
- **No `Store<ProjectState>` reducer pattern from `mwt`.** Jobfinder's CLI and GUI are stateless per call — sharing through the `Jobmatch/` library + DTO contracts is enough.

## When in doubt

- Re-read [`docs/prd.md`](docs/prd.md) for principle.
- Re-read [`docs/requirements.md`](docs/requirements.md) for the contract.
- Re-read [`docs/mwt-tool-analysis.md`](docs/mwt-tool-analysis.md) for the architecture pattern.
