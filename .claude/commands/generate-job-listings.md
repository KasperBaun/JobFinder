---
description: Fetch, deduplicate, rank, and write the top-N job shortlist.
argument-hint: "[--portal NAME] [--top N]"
---

Run the `jobmatch` CLI `listings` subcommand. It pulls from every enabled portal, deduplicates, ranks against the active skillset, and writes `data/top_jobs.md` plus `data/ranked_listings.json`.

From the repo root, execute:

```
dotnet run --project src/Jobmatch.Cli -- listings $ARGUMENTS
```

Phase 1 stub — the pipeline itself is wired in Phase 3 (fetch + dedupe) and Phase 4 (rank).
