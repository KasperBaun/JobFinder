# jobmatch

A local, configurable job-matching tool. Define your skillset in one file, list the portals you care about in another, run one command, get a ranked shortlist.

See [PRD.md](./PRD.md) for what it does and why, and [implementation-plan.md](./implementation-plan.md) for how it's built.

## Status

**All five build phases complete.** Skillset authoring, portal fetch (API / RSS / manual import / HTML via Playwright), deduplication, ranking with honest reasoning, and the connectivity-aware verifier are all wired up. See `implementation-plan.md` §9 for the per-phase deliverables and `PRD.md` §9 for the acceptance criteria.

## Prerequisites

- .NET 10 SDK (pinned in `global.json`)

## Quickstart

```bash
# Copy the example configs and edit them
cp config/skillset.example.md config/skillset.md
cp config/portals.example.yml config/portals.yml

# Show the CLI help
dotnet run --project src/Jobmatch.Cli -- --help

# Run individual subcommands (all stubs in Phase 1)
dotnet run --project src/Jobmatch.Cli -- skillset
dotnet run --project src/Jobmatch.Cli -- listings
dotnet run --project src/Jobmatch.Cli -- verify
```

## Build and test

```bash
dotnet build
dotnet test
```

## Slash commands

Three Claude Code slash commands in `.claude/commands/` wrap the CLI subcommands:

| Slash command | What it does |
|---|---|
| `/generate-skillset` | Author or refresh `config/skillset.md` |
| `/generate-job-listings` | Fetch, dedupe, rank, write `data/top_jobs.md` |
| `/verify-config` | Validate config files and portal connectivity |

Each slash command shells out to the matching CLI subcommand — identical behaviour from the terminal.

## Repository layout

Highlights (full tree in `implementation-plan.md` §2):

- `src/Jobmatch/` — library (models, parsing, adapters, ranking, output)
- `src/Jobmatch.Cli/` — CLI entrypoint on Spectre.Console.Cli
- `tests/Jobmatch.Tests/` — xUnit tests
- `config/` — user configs (`*.example.*` committed; active copies gitignored)
- `data/` — runtime outputs (gitignored)
