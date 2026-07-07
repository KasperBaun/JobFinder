# Requirements

One-line requirements for `jobfinder`. Each line is a single thing the system should do, intentionally short. Detail and rationale belong in [`prd.md`](./prd.md); per-task specs belong in [`tasks/`](./tasks/).

## Identity & per-user data

- **R-001** The system should identify each user by email and keep all of that user's state under `data/<email>/`, never co-mingling users.
- **R-002** The system should default a user's email from `git config user.email` and accept an explicit override (env `JOBFINDER_USER`, or a setting in the GUI).
- **R-003** The system should treat `data/<email>/` as private and gitignored — nothing in it is ever committed.
- **R-004** When jobfinder runs outside a git repository (no `.git` anchor up the directory tree), the per-user data directory resolves to `%LOCALAPPDATA%/jobfinder/data/<email>/` (Windows) or `~/.local/share/jobfinder/data/<email>/` (Unix) rather than the current working directory.

## Skillset

- **R-010** The system should assist a user with remembering their skillset and preferences to use in future job searches.
- **R-011** The system should let a user author or refresh their skillset interactively, from a CV file, or from a CV URL. The CV path runs the local AI model over the CV text (pasted, uploaded `.pdf`/`.txt`/`.md`, or fetched from a URL) as a background extraction the GUI polls, and only prefills the profile form — persistence always requires the user's explicit save (R-012).
- **R-012** The system should refuse to silently overwrite an existing skillset and instead surface a diff for confirmation.
- **R-013** The system should ship a generic example skillset so a new user sees the expected shape.

## Search criteria & providers

- **R-020** The system should let a user list job providers (e.g. JOBNET) they want to pull from, and let them enable or disable each without removing it.
- **R-021** The system should accept six provider access types: public API, RSS/Atom feed, server-rendered HTML, manual import, TeamTailor career sites (sitemap + JSON-LD), and HR-Manager.net customer lists.
- **R-022** The system should fetch only providers explicitly configured by the user — never the open web.
- **R-023** The system should let a user view their currently configured providers and current search criteria from the GUI.
- **R-024** The system should ship a curated provider catalog as part of the application bundle so a new user can run a search on first launch with no setup.
- **R-090** The system should let a user add and remove their own job sources from the GUI by pasting a careers-page or job-feed URL: recognised ATS boards (Greenhouse, Ashby, Lever, SmartRecruiters, TeamTailor, HR-Manager) and RSS feeds are configured automatically from the URL, unrecognised addresses fall back to guided manual import, and the candidate can be live-tested before saving. User-added sources persist per-user in `data/<email>/user-providers.json` (ids ≥ 10000) and never modify the shipped catalog; only sources the user added themselves can be removed. When a candidate duplicates an existing feed backend (e.g. jobindex/it-jobbank), the system surfaces a non-blocking warning before it's saved.
- **R-025** The system should let a portal config inject static field values (e.g. company name) into every produced listing — used by per-company ATS boards that don't carry the company in their payload.
- **R-026** The system should support api providers that require `method: post` with a JSON request body (`body_template:`) and that embed config values as `{key}`-style placeholders in the endpoint URL (substituted from `query_params`, with the consumed key removed from the query string).
- **R-027** The system should support paginated api providers via a `pagination:` config block (`param`, `start`, `step`, optional `size_param` + `size`, `max_pages` safety cap), incrementing the named param per request and stopping on empty page, partial page (count < `size`), or `max_pages`. Pagination params route into `body_template` for POST providers, into `query_params` for GET.
- **R-028** The system should honour a portal's declared `rate_limit_rps` by spacing successive HTTP calls (within a fetch — across pages, or across HTML/RSS retries) so the configured per-second cap is not exceeded. Set `rate_limit_rps: 0` to disable.

## Running a search

- **R-030** The system should run a search across all enabled providers in a single user-initiated action, deduplicate, rank against the user's skillset, and produce a top-N shortlist.
- **R-031** The system should keep one provider's failure (timeout, rate limit, parse error) from killing the rest of the run.
- **R-032** The system should persist raw per-provider results so a run can be re-examined or re-ranked without re-fetching.
- **R-033** The system should write a human-readable top-N report alongside the machine-readable ranked output.
- **R-034** The system should let the user cap the shortlist by minimum score and by N.
- **R-035** The system should expose, per run, every dropped listing and the explicit reason it was dropped (disqualifier match, score below threshold, beyond top-N, above max age, missing required primary stack).
- **R-036** The system should run a search as a background job (Hangfire, durable SQLite-backed queue) decoupled from the HTTP request, so the run survives the user navigating away, reloading the page, and restarting the host process. Navigating away or closing the tab must not cancel the run. (Deliberate exception to the general "no background jobs / no DB" rule, scoped to one user-initiated run; not a daemon or scheduler.)
- **R-037** The system should stream live run progress to the GUI over SSE — current snapshot on connect (replay), then updates — and let the GUI reconnect to an in-flight run by id after a reload. A disconnected viewer must not affect the run.
- **R-038** The system should let the user cancel an in-flight (queued or running) search from the GUI.
- **R-093** The system should run the local AI-model download decoupled from the HTTP request that starts it, holding live progress in a process-singleton so the download survives the user navigating away and reloading the page; the GUI reconnects by polling model status and shows current progress. Starting a download is idempotent — a repeat request while one is already running is a no-op — and partial bytes are written to a temp file that is promoted only on completion, so an interrupted transfer never reports as a present model.

## Ranking

- **R-040** The system should rank listings using user-controlled weights across primary stack, secondary stack, seniority, location/remote, domain, and freshness.
- **R-041** The system should let a user declare deal-breaker keywords that match the listing's title or company name (description excluded — too many false positives like "junior-to-senior team" zeroing real senior roles) and zero its score on hit.
- **R-042** The system should explain every ranked match: which signals fired, which didn't, and a one-line note — never invented reasoning.
- **R-043** The system should never invent a positive signal; when a signal can't be evaluated (missing field), it should say so.
- **R-044** The system should record a per-component score breakdown (weighted contribution of primary stack, secondary stack, seniority, location/remote, domain, freshness, disqualifier penalty, non-engineering-title penalty, and preferred-company bonus) for every ranked listing.
- **R-087** The system should multiply a listing's score by a configurable factor (default 0.2) when its title looks clearly non-engineering — Product/Project/Account Manager, Marketing/Sales/Finance roles, Customer Success, Recruiter, Data/Business/Fraud Analyst, etc. — to prevent incidental tech-keyword hits in the description from dragging non-engineering roles into the shortlist. Engineer/Engineering/Developer/Architect/SRE/DevOps in the title overrides this gate.
- **R-088** The system should give full credit (configurable, default 1.0) to listings whose seniority is one level off from the user's (mid↔senior, senior↔lead, junior↔mid). The IT market overcounts "Senior" — strict matching drags down most matches for users who self-classify mid-with-experience. Reasoning notes still distinguish "adjacent" from "fits" so the user knows the level isn't an exact match.
- **R-089** The system should support per-portal body enrichment for RSS feeds (`enrichBody: true` in the catalog): after parsing the feed, fetch each item's linked HTML page sequentially with a small delay (~5rps) and merge the visible text into the listing's description before ranking. Failures don't drop the listing — the RSS-only version is kept and the failure logged.
- **R-090** The system should match the user's declared cities and metro areas across Danish/English language variants (Copenhagen ↔ København / Kbh / Cph; Aarhus ↔ Århus; Aalborg ↔ Ålborg; Greater Copenhagen ↔ Storkøbenhavn / Hovedstaden; Helsingør ↔ Elsinore) so a Danish-language listing for "København" still earns the City tier weight when the user declared "Copenhagen".
- **R-091** The system should let a user declare preferred companies (employers they'd love to work for) in their skillset; a listing whose company name matches one gets its score multiplied by a configurable boost (`preferred_company_boost`, default 1.25, clamped at 1.0). Matching is against the company name only — a listing that merely mentions a preferred company in its description gets no boost — and a deal-breaker hit still zeroes the score. The preferred list is also surfaced to the LLM judge.

## History & feedback

- **R-050** The system should remember every previous search run with timestamp, providers fetched, listings count, and shortlist count.
- **R-055** The system should record every run as a `JobSearch` with an explicit lifecycle state (queued, running, succeeded, failed, cancelled, interrupted) and a timestamped timeline, so abandoned and failed runs are visible in history — not just completed ones. A run whose worker is lost (host killed mid-run) should surface as interrupted.
- **R-051** The system should let a user view previous searches in the GUI, including how many listings the user marked as a good match per run.
- **R-052** The system should let a user mark a listing as a good match (or not) so the ranking algorithm can be improved over time.
- **R-053** The system should keep marks per user, per listing, scoped to the run that produced them.
- **R-094** The system should let a user attach a short free-form reason to a mark ("I'm not a student", "wrong stack") — editable in place, and dropped when the mark is flipped or cleared.
- **R-095** The system should feed marked listings from previous runs — with their reasons, plus the free-form notes in curated example files — to the LLM judge as few-shot examples on subsequent runs, so a repeated mistake can be corrected by marking it once.
- **R-054** The system should let a user supply seed listings — hand-picked archetypes of the kind of role they want, independent of any prior search run — so the ranking algorithm has positive signal to learn from from day one.
- **R-055** The system should accept seed listings of either polarity (liked or disliked) and treat them as input to ranking improvements, not as fixtures or test data.

## Verification

- **R-060** The system should let a user verify that config files exist, parse, point at reachable endpoints, and have manual imports where required.
- **R-061** The system should report verification results as pass / warn / fail per check, never crashing on a single failure.
- **R-062** The system should persist a verification report alongside an in-app summary.

## Entry points

- **R-070** The system should run as a single binary that launches a browser-based desktop app on start.
- **R-071** The system should auto-launch a browser pointed at an ephemeral local server when started.
- **R-072** The system should expose all of its capabilities through the same browser interface — there is no parallel headless or text-mode surface in v1.
- **R-073** The system should detect server disconnect from the browser and tell the user the app is no longer attached.
- **R-074** The system should be distributable as a self-contained Windows installer, built automatically on push to `main` and reproducible locally, that requires no .NET or Node runtime on the target machine.
- **R-075** The system should, on first run when no data location has been established, require the user to confirm where their data is stored (offering an editable suggestion) before creating or writing anything, and persist that choice for subsequent runs.
- **R-076** The system should, on first run, guide the user to create their own profile; the profile step is skippable, and while no profile exists the Profile and Search surfaces should prompt the user to finish it instead of failing.

## Non-functional

- **R-080** The system should be local-only — no telemetry, no cloud calls, all data on the user's disk.
- **R-081** The system should be on-demand only — no daemon, no scheduled runs.
- **R-082** The system should be generic — no role, country, employer, or stack hard-coded in executable code; every preference lives in user data.
- **R-083** The system should never bypass anti-bot measures; sites that block automation are supported only via the manual-import provider type.
- **R-084** The system should log honestly: provider name, error class, status code on failure; counts and top score on success.

## Provider catalog & per-user state

- **R-085** The system should expose a filterable, sortable view of every deduped listing in a run, with at least: portal, score range, posting age, primary/secondary stack hit, mark state, shortlist membership, and free-text search across title and company.
- **R-086** The system should ship the provider catalog as part of the application bundle (read-only at runtime) and store only per-user enable-state overrides (opt-ins for catalog-disabled providers, opt-outs for catalog-enabled ones) and provider secrets under `data/<email>/`.
- **R-087** The system should let a user export their complete per-user state (skillset, provider overrides and secrets, marks, ranking override, and full search history) under `data/<email>/` as a single downloadable archive, excluding the re-downloadable LLM model and transient files.
- **R-088** The system should let a user import a previously exported archive into the active user's directory, validating its format, backing up the current state before overwriting, preserving the local LLM model, and refusing archives written by a newer format version or with an unsafe path.
