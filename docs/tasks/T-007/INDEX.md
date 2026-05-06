# T-007 — Portal API research progress

Per-portal worksheets live in this directory. The table below is the
roll-up; each row links to the worksheet with full Findings + Verdict
+ Stub block.

Status legend: `open` (no work yet) · `wip` (claimed) · `done`
(verdict written). Verdict legend: `api` · `rss` · `html` · `manual` ·
`dead` · `_tbd_`.

| Portal | Status | Verdict | Worksheet / note |
|---|---|---|---|
| Jobindex.dk | done | rss | [jobindex.md](jobindex.md) — `/jobsoegning.rss`, undocumented but works, accepts `q=` |
| Jobnet.dk | done | manual | [jobnet.md](jobnet.md) — STAR cert auth still required in 2026; existing disabled stub stays |
| LinkedIn | done | manual | [linkedin.md](linkedin.md) — Talent Solutions still partner-gated, closed to new applicants |
| Indeed.dk | done | dead | [indeed.md](indeed.md) — single-source XML feeds shut off Mar 31 2026 |
| Ofir.dk | done | dead | [ofir.md](ofir.md) — whole-host 301 → jobindex.dk; covered by `jobindex-rss` |
| Stepstone.dk | done | manual | [stepstone.md](stepstone.md) — `api.stepstone.com` is push-only B2B; no consumer feed |
| Jobzonen.dk | done | dead | [jobzonen.md](jobzonen.md) — duplicates jobsearch.dk (relaunched on top in 2019) |
| Careerjet.dk | done | api | [careerjet.md](careerjet.md) — fits existing `ApiAdapter`; needs free `affid` |
| Jooble.org | done | api | [jooble.md](jooble.md) — `ApiAdapter` POST + `body_template:` + `{api_key}` extension landed; ships disabled, register for an api_key to enable |
| Jobsearch.dk | done | rss | [jobsearch.md](jobsearch.md) — `/feed/job-annoncer`, canonical for the Jobzonen pool |
| Monster.dk | done | dead | [monster.md](monster.md) — DK domain abandoned post-Randstad, no developer page |
| Workindenmark.dk | done | dead | [workindenmark.md](workindenmark.md) — frontend over Jobnet/EURES; no independent feed |
| Jobbank.dk | done | manual | [jobbank.md](jobbank.md) — no RSS / no API / no partner program |
| EURES | done | manual | [eures.md](eures.md) — backend exists but CSRF-gated; no anonymous token |
| IT-jobbank.dk | done | rss | [it-jobbank.md](it-jobbank.md) — same Jobindex backend, IT-scoped slice |
| TechJob.dk | done | manual | [techjob.md](techjob.md) — Drupal, robots.txt blocks AI crawlers, only empty articles RSS |
| Recruit IT | done | html | [recruit-it.md](recruit-it.md) — stable selectors at `recruit-it.dk/ledige-stillinger-danmark` |
| DevJobsScanner | done | dead | [devjobsscanner.md](devjobsscanner.md) — no feed, redundant with ATS coverage |
| The Hub | done | api | already wired in `src/config/portals.example.yml` |
| Stack Overflow / GitHub Jobs | done | dead | both shut down 2022; nothing to wire |

## What shipped to portals.example.yml

7 new disabled stub blocks under a "T-007 — DK research findings" section:

- `jobindex-rss` (rss) — undocumented public feed; `q=` keyword filter
- `it-jobbank-rss` (rss) — same Jobindex backend, IT-scoped (enable one or the other, not both)
- `jobsearch-dk` (rss) — open RSS 2.0; canonical for the Jobzonen pool
- `careerjet-dk` (api) — register `affid` to enable; fits existing GET adapter
- `jooble` (api) — disabled until you register an api_key; adapter POST/`body_template:`/`{api_key}` extension shipped (R-026)
- `recruit-it` (html) — Playwright-rendered scrape; agency name stamped via `static_fields`
- `stepstone-dk` (manual) — explicit CSV-import note since `/jobsoegning.rss` 404s here unlike the rest of the Jobindex family

## T-007 follow-ups (separate tasks)

- **Recruit IT Playwright smoke test** — the html stub uses `:scope` as `link_selector` to target the wrapping `<a>`. Verify Playwright resolves `:scope` inside `IElementHandle.QuerySelectorAsync` before flipping `enabled: true`; otherwise extend `HtmlAdapter` to read attributes from the matched list element directly.
- **Recruit IT location parsing** — location is rendered as a plain text node next to an icon with no wrapper class; `location_selector` is intentionally omitted and listings will have null location until the markup changes.
- **Jobindex / IT-jobbank dedupe** — both feed off the same Jobindex backend; enabling both wastes calls. Consider a config-time hint or a UX warning if both are enabled at once.
- **Jobsearch.dk shape-poor RSS** — items expose only `title` / `description` / `link`; no `pubDate`, no structured company/location. May need a parser to extract company/location from the title or URL slug `/{role}/{city}/{id}`.

## Method recap

See [`../T-007-portal-api-research.md`](../T-007-portal-api-research.md)
for the full playbook. Short version per portal:

1. Hit the homepage; look for "Developer", "API", "Partners", "RSS", "For employers" links.
2. Try `/rss`, `/feed`, `/jobs.rss`, `/job/rss`.
3. Open a search page in the browser, watch the network tab for an XHR returning JSON.
4. Web-search `"<portal> api"` and `"<portal> rss feed"`.
5. Record the result in the worksheet (Findings → Verdict → Stub block).
6. If `api` or `rss`: append a disabled stub block to `src/config/portals.example.yml` matching the `adzuna-dk` pattern.
7. Update the row above (`open` → `done`, write the verdict).
