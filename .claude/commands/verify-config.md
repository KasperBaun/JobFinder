---
description: Validate config files and portal connectivity.
argument-hint: "[--offline]"
---

Run the jobmatch config verifier. This is the fastest way to catch typos or broken URLs before spending time on a full listings run.

The verifier checks, in order, without crashing on any individual failure:

1. Required files exist (`config/skillset.md`, `config/portals.yml`, `config/ranking.yml`).
2. Skillset parses and has a non-empty primary stack.
3. Portal config parses and each enabled portal has the fields its type requires.
4. Ranking weights sum to 1.0 ± 0.01 and `top_n >= 1`.
5. Enabled `manual` portals have at least one matching import file on disk.
6. Enabled `html` portals warn about Playwright browser installation.
7. Enabled `api`/`rss` portals respond to a minimal test request (short timeout). Skipped with `--offline`.

Each check reports ✓ pass, ! warn, or ✗ fail. A non-zero exit code means something must be fixed. The full result is also written to `data/verification-report.md`.

## What to do

1. Run the subcommand below.
2. If any check fails, call out which and suggest the fix (e.g. "add the missing endpoint to portals.yml", "weights currently sum to 1.05 — adjust one of them down by 0.05").
3. If all checks pass or only warnings remain, say so and remind the user that `/generate-job-listings` is next.

## Command

From the repo root:

```
dotnet run --project src/Jobmatch.Cli -- verify $ARGUMENTS
```

Pass `--offline` to skip connectivity checks.
