# T-007 — Portal API research (DK general + tech)

## Why

Today's seeded providers cover a handful of per-company ATS boards
(Greenhouse) plus `thehub` and `remotive`. The Danish market has many
more portals — some with public APIs, some with RSS, some hard-blocked.
We don't have a record of *which is which*, so every time the seed is
revisited, the same evaluation gets redone. The user supplied a
ranked list of the largest general and tech-specific DK portals; this
task evaluates each one and turns the result into config (or a clear
"manual only" note) so future iterations start from a known baseline.

## Outcome

For every portal in scope, the repo records one of:

- **api** — endpoint shape, auth, rate limit, response mapping → add a
  disabled (or enabled, if no auth) stub block to
  `src/config/portals.example.yml` matching the `adzuna-dk` pattern.
- **rss** — feed URL → add an `rss` adapter stub block.
- **manual** — reason automation isn't viable → add a `manual` stub
  with a short `notes:` block (same pattern as `jobindex`/`linkedin`).
- **dead** — shut down, paywalled, or pure duplicate of another portal
  → no block; verdict captured in the worksheet so the next agent
  doesn't re-research it.

## Parallelization

Each portal in scope has its own worksheet under
[`T-007/`](T-007/). The progress index lives at
[`T-007/INDEX.md`](T-007/INDEX.md) — that's the source of truth for
who's done and who's open.

To assign work to N parallel agents:

1. Each agent picks one or more `open` worksheets from
   [`T-007/INDEX.md`](T-007/INDEX.md).
2. Agent does the research per **Method** below.
3. Agent fills in **Findings** + **Verdict** + (optional) **Stub
   block** in the worksheet.
4. Agent updates the corresponding row in `INDEX.md` (`open` → `done`
   plus the verdict).
5. When a viable `api` or `rss` is found, the agent appends the stub
   block to `src/config/portals.example.yml` (disabled by default
   unless the endpoint is open and free).

Worksheets are file-isolated, so two agents can run in parallel
without contention. `INDEX.md` is the only file with possible edit
overlap; agents update one row at a time and commits stay clean.

## Method

For each unresolved portal, the agent doing this work should:

1. Hit the homepage and look for a "Developer", "API", "Partners",
   "RSS", or "For employers" link.
2. Try the obvious RSS URLs (`/rss`, `/feed`, `/jobs.rss`,
   `/job/rss`).
3. Open a job-search page in the browser and watch the network panel
   for an XHR/fetch call returning JSON — many portals have an
   undocumented internal API. *If* it returns JSON for unauthenticated
   callers, document it (with the caveat that internal APIs can change
   without notice).
4. Search for `"<portal> api"` and `"<portal> rss feed"` on the open
   web; check if any partner/affiliate program exists.
5. If a viable endpoint is found, add a disabled stub to
   `src/config/portals.example.yml` with mapping + a `notes:` block
   describing how to enable it. Don't ship secrets.
6. If not, write a one-line entry in the worksheet (`manual` or
   `dead`) so the answer survives.

## Out of scope

- **Building a new adapter type.** Existing `api`, `rss`, `html`,
  `manual` cover everything reasonable. If a portal needs something
  else (e.g. POST + JSON body for Jooble — see that worksheet),
  capture the requirement and stop.
- **Bypassing anti-bot or auth.** If a portal needs a logged-in
  session or rotates tokens, mark it `manual` and move on. CLAUDE.md
  is explicit.
- **Expanding aggregators-of-aggregators.** Portals like Indeed and
  Careerjet *re-list* jobs from elsewhere — wiring them on top of
  per-company Greenhouse boards mostly produces duplicates. Document
  but don't enable by default.
- **Per-company ATS enumeration for Jobindex's 500+ boards.** Out of
  scope here; T-005 already covers seeding individual ATS boards.
- **Real Adzuna/Jooble/Careerjet keys committed to the repo.**
  Placeholders + registration instructions only.

## Tests

No new tests strictly required — this is a research + config task. If
a new portal block is added with a non-trivial `response_mapping`,
add a round-trip test in `PortalConfigLoaderTests` covering the new
shape.

## Requirements touched

R-020 (provider list), R-021 (provider types), R-024 (example config).
No new R needed unless the research surfaces a contract gap (e.g. a
portal that needs a request body in a new shape — see the Jooble
worksheet, which currently flags this as a possible adapter
extension).
