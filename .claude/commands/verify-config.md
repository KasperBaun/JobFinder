---
description: Validate config files and (from Phase 5) portal connectivity.
---

Run the `jobmatch` CLI `verify` subcommand. It checks:

- required config files exist (`config/skillset.md`, `config/portals.yml`, `config/ranking.yml`)
- the skillset parses and has a non-empty primary stack
- each enabled portal has the fields its type requires
- ranking weights sum to 1.0 ± 0.01
- enabled `manual` portals have at least one matching import file
- enabled `html` portals have their browser-automation component installed
- enabled `api`/`rss` portals respond to a minimal test request (Phase 5)

From the repo root, execute:

```
dotnet run --project src/Jobmatch.Cli -- verify
```

Phase 1 stub — real checks arrive in Phase 2 (structural) and Phase 5 (connectivity).
