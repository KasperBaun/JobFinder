# jobfinder

A personal job-search assistant that runs on your laptop.

You describe your skills, preferences, and dealbreakers in one place. You list the job sites you want to check. You open the desktop app and get a short, ranked list of openings that actually fit you. Over time, you mark the ones you liked, and the system learns to put more like those at the top.

Everything stays on your machine. No sign-up, no cloud account, no telemetry. Your skillset, your provider list, your search history, and your "good match" marks all live in a folder under your email on your own disk.

## What you get

- **A single skillset.** One file describes who you are professionally — stack, seniority, location, deal-breakers. Edit it any time; every search uses the latest version.
- **A list of providers.** Pick the job sites you actually use (e.g. JOBNET). Toggle them on and off without touching anything else.
- **A ranked shortlist on demand.** One click — the tool checks every enabled provider, removes duplicates, scores everything against your skillset, and surfaces the top matches with honest reasoning per match.
- **A history of your runs.** Every search is remembered. You can look back at last Sunday's run, see which listings came back, and see how many you marked as a real fit.
- **A way to teach it.** Mark listings as good matches (or not). The system uses those signals to improve future rankings.

## How you use it

Launch the binary and a local app opens in your browser. View your providers, your search criteria, and your search history at a glance. Run searches and mark good matches with a click. The browser closes when you're done; nothing keeps running in the background.

## Where your data lives

Everything personal lives under `data/<your-email>/` on your own machine. That folder is never committed to git, never sent anywhere, never shared. If you delete it, the tool forgets everything about you and starts fresh.

## Where to look next

- [`docs/prd.md`](./docs/prd.md) — what this product is and who it's for.
- [`docs/requirements.md`](./docs/requirements.md) — the one-line requirement list.
- [`docs/mwt-tool-analysis.md`](./docs/mwt-tool-analysis.md) — the architecture pattern the desktop app follows.
- [`todo.md`](./todo.md) — what's done, what's in flight, and what's next.

## Status

The fetch / dedupe / rank pipeline lives in the `Jobmatch` library. The desktop app, the per-user `data/<email>/` layout, and the "mark as good match" feedback loop are the next milestones — see `todo.md`.
