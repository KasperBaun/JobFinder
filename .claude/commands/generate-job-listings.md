---
description: Fetch, deduplicate, rank, and write the top-N job shortlist.
argument-hint: "[--portal NAME] [--top N]"
---

Run the full jobmatch pipeline: every enabled portal in `config/portals.yml` is fetched, results are deduplicated across portals, ranked against `config/skillset.md`, and written to `data/top_jobs.md` plus `data/ranked_listings.json`.

## Before running

- Check that `config/skillset.md`, `config/portals.yml`, and `config/ranking.yml` exist. If any is missing, instruct the user to run `/generate-skillset` or copy the `.example` files.
- If the user wants only one portal, pass `--portal <name>`.
- If the user wants a different shortlist size than `ranking.yml`'s `top_n`, pass `--top <N>`.
- If the shortlist is empty and the summary shows a hint, broaden the threshold with `--min-score <0.0–1.0>` (e.g. `--min-score 0.10`).
- This may take a minute for HTML portals (Playwright boots a headless Chromium). API and RSS portals are typically fast.

## What to do

1. Run the subcommand below.
2. Watch the per-portal log. Green ✓ means success; red ✗ means the adapter failed on that one portal — the run continues either way (FR-019).
3. When it finishes, read the Summary block (fetched counts, dedupe total, shortlist size, top score) and relay it to the user.
4. Offer to open `data/top_jobs.md` so the user can review the ranked shortlist.

## Command

From the repo root:

```
dotnet run --project src/Jobmatch.Cli -- listings $ARGUMENTS
```

Common invocations:
- `/generate-job-listings` — run everything enabled
- `/generate-job-listings --portal jobnet` — one portal only
- `/generate-job-listings --top 5` — smaller shortlist
- `/generate-job-listings --min-score 0.10` — broaden the shortlist threshold
