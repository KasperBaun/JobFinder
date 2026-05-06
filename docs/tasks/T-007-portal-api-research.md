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

For every portal in the two lists below, the repo records one of:

- **API** — endpoint shape, auth, rate limit, response mapping → add a
  disabled (or enabled, if no auth) stub block to
  `src/config/portals.example.yml` matching the `adzuna-dk` pattern.
- **RSS** — feed URL → add an `rss` adapter stub block.
- **Manual** — reason automation isn't viable → add a `manual` stub
  with a short `notes:` block (same pattern as `jobindex`/`linkedin`).
- **Dead / unusable** — shut down, paywalled, or duplicates another
  portal in the seed → don't add a block; mention in this task spec
  under *Findings* so the next agent doesn't re-research it.

A short *Findings* table at the bottom of this file is updated as each
portal is checked, so the research is captured in the repo and not just
in commit messages.

## Scope

### Research targets

**General DK portals** (from the supplied ranking):

1. **Jobindex.dk** — already classified `manual` (no public API, ToS).
   Verify: any partner/affiliate feed in 2026? Note in *Findings* and
   stop.
2. **Jobnet.dk** — already classified `manual`/disabled `api` skeleton.
   Verify: still cert-auth via STAR? Any change after the 2024–25
   public-sector data initiatives? Update notes.
3. **LinkedIn** — already classified `manual`. Verify: any new
   public-job-search API tier? Almost certainly still no, but record.
4. **Indeed.dk** — Indeed Publisher API was deprecated. Verify current
   state of the XML feed / partner program / Job Search API.
5. **Ofir.dk** — owned by Jobindex since Jan 2025. Check for RSS feed
   (historically had one) or affiliate API.
6. **Stepstone.dk** — Jobindex group. Check for RSS or partner feed.
7. **Jobzonen.dk** — check for RSS / partner feed.
8. **Careerjet.dk** — Careerjet Connect (Affiliate) JSON API on
   application. Document the apply step + endpoint shape.
9. **Jooble.org** — public API exists on registration (free tier).
   Document endpoint + auth.
10. **Jobsearch.dk** — backs Jobzonen; check whether the underlying
    feed is the same or separate.
11. **The Hub** — already wired (`thehub.io/api/jobs`). No work.
12. **Monster.dk** — historically had a partner API (now Randstad).
    Verify 2026 status.
13. **Workindenmark.dk** — public sector under the
    Beskæftigelsesministeriet. Likely overlaps with EURES; check for a
    direct feed.
14. **Jobbank.dk (Akademikernes)** — check for RSS / partner feed.
15. **EURES (eures.europa.eu)** — EU open-data feeds available;
    document the DK-filtered endpoint.

**Tech-specific DK portals**:

1. **IT-jobbank.dk** — Jobindex-owned; same story as #1, but verify a
   tech-only RSS/feed exists.
2. **TechJob.dk** (ex-Jobfinder, Teknologiens Mediehus) — check for
   RSS/API. Brand was relaunched recently — feed URL may have changed.
3. **The Hub** — already wired.
4. **Recruit IT (recruit-it.com)** — small agency. List size is in the
   tens; HTML scrape is the only path. Document selector candidates or
   mark as low-value.
5. **DevJobsScanner.com** — aggregator; redistribution rights unclear.
   Default to *Manual / dead* unless an explicit feed exists.
6. **Stack Overflow Jobs / GitHub Jobs** — both shut down (2022). Mark
   *Dead*. No further action.
7. **Workindenmark.dk** — covered above.

### Method (per portal)

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
   `portals.example.yml` with mapping + a `notes:` block describing
   how to enable it. Don't ship secrets.
6. If not, write a one-line entry in the *Findings* section below
   (`Manual` or `Dead`) so the answer survives.

### Out of scope

- **Building a new adapter type.** Existing `api`, `rss`, `html`,
  `manual` cover everything reasonable. If a portal needs something
  else, capture the requirement and stop.
- **Bypassing anti-bot or auth.** If a portal needs a logged-in
  session or rotates tokens, mark it `manual` and move on. CLAUDE.md is
  explicit.
- **Expanding aggregators-of-aggregators.** Portals like Indeed and
  Careerjet *re-list* jobs from elsewhere — wiring them on top of
  per-company Greenhouse boards mostly produces duplicates. Document
  but don't enable by default.
- **Per-company ATS enumeration for Jobindex's 500+ boards.** Out of
  scope here; T-005 already covers seeding individual ATS boards.
- **Real Adzuna/Jooble keys committed to the repo.** Placeholders +
  registration instructions only.

### Tests

No new tests strictly required — this is a research + config task. If
a new portal block is added with a non-trivial `response_mapping`,
add a round-trip test in `PortalConfigLoaderTests` covering the new
shape.

## Requirements touched

R-020 (provider list), R-021 (provider types), R-024 (example config).
No new R needed unless the research surfaces a contract gap (e.g. a
portal that needs auth headers in a new shape).

## Findings

_(Filled in as each portal is checked. One line per portal:
`<name> — <Api | Rss | Manual | Dead> — <one-line note>`.)_

| Portal | Verdict | Note |
|---|---|---|
| Jobindex.dk | _tbd_ | Already manual; verify no partner program in 2026. |
| Jobnet.dk | _tbd_ | Already disabled `api` stub; verify STAR cert-auth still required. |
| LinkedIn | _tbd_ | Already manual; verify still no public job-search API. |
| Indeed.dk | _tbd_ | Publisher API deprecated — verify current state. |
| Ofir.dk | _tbd_ | Jobindex-owned since 2025; check for RSS. |
| Stepstone.dk | _tbd_ | Jobindex group; check for RSS / partner feed. |
| Jobzonen.dk | _tbd_ | Check for RSS / partner feed. |
| Careerjet.dk | _tbd_ | Careerjet Connect Affiliate API — document apply step. |
| Jooble.org | _tbd_ | Public API on registration — document endpoint. |
| Jobsearch.dk | _tbd_ | Backs Jobzonen — same feed or separate? |
| The Hub | Api | Already wired (`thehub.io/api/jobs`). |
| Monster.dk | _tbd_ | Historic partner API — verify 2026. |
| Workindenmark.dk | _tbd_ | Likely overlaps EURES — check direct feed. |
| Jobbank.dk | _tbd_ | Check for RSS / partner feed. |
| EURES | _tbd_ | EU open-data DK-filtered endpoint — document. |
| IT-jobbank.dk | _tbd_ | Jobindex-owned; check tech-only RSS. |
| TechJob.dk | _tbd_ | Ex-Jobfinder; brand relaunched — check feed URL. |
| Recruit IT | _tbd_ | Agency; HTML scrape only. Low value. |
| DevJobsScanner | _tbd_ | Aggregator; redistribution rights unclear. |
| Stack Overflow / GitHub Jobs | Dead | Both shut down 2022. No action. |
