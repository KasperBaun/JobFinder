# Requirements

One-line requirements for `jobfinder`. Each line is a single thing the system should do, intentionally short. Detail and rationale belong in [`prd.md`](./prd.md); per-task specs belong in [`tasks/`](./tasks/).

## Identity & per-user data

- **R-001** The system should identify each user by email and keep all of that user's state under `data/<email>/`, never co-mingling users.
- **R-002** The system should default a user's email from `git config user.email` and accept an explicit override (env `JOBFINDER_USER`, or a setting in the GUI).
- **R-003** The system should treat `data/<email>/` as private and gitignored — nothing in it is ever committed.

## Skillset

- **R-010** The system should assist a user with remembering their skillset and preferences to use in future job searches.
- **R-011** The system should let a user author or refresh their skillset interactively, from a CV file, or from a CV URL.
- **R-012** The system should refuse to silently overwrite an existing skillset and instead surface a diff for confirmation.
- **R-013** The system should ship a generic example skillset so a new user sees the expected shape.

## Search criteria & providers

- **R-020** The system should let a user list job providers (e.g. JOBNET) they want to pull from, and let them enable or disable each without removing it.
- **R-021** The system should accept four provider access types: public API, RSS/Atom feed, browser-rendered HTML, manual import.
- **R-022** The system should fetch only providers explicitly configured by the user — never the open web.
- **R-023** The system should let a user view their currently configured providers and current search criteria from the GUI.
- **R-024** The system should ship a generic example provider list so a new user sees the expected shape.

## Running a search

- **R-030** The system should run a search across all enabled providers in a single user-initiated action, deduplicate, rank against the user's skillset, and produce a top-N shortlist.
- **R-031** The system should keep one provider's failure (timeout, rate limit, parse error) from killing the rest of the run.
- **R-032** The system should persist raw per-provider results so a run can be re-examined or re-ranked without re-fetching.
- **R-033** The system should write a human-readable top-N report alongside the machine-readable ranked output.
- **R-034** The system should let the user cap the shortlist by minimum score and by N.

## Ranking

- **R-040** The system should rank listings using user-controlled weights across primary stack, secondary stack, seniority, location/remote, domain, and freshness.
- **R-041** The system should let a user declare deal-breaker keywords that zero a listing's score.
- **R-042** The system should explain every ranked match: which signals fired, which didn't, and a one-line note — never invented reasoning.
- **R-043** The system should never invent a positive signal; when a signal can't be evaluated (missing field), it should say so.

## History & feedback

- **R-050** The system should remember every previous search run with timestamp, providers fetched, listings count, and shortlist count.
- **R-051** The system should let a user view previous searches in the GUI, including how many listings the user marked as a good match per run.
- **R-052** The system should let a user mark a listing as a good match (or not) so the ranking algorithm can be improved over time.
- **R-053** The system should keep marks per user, per listing, scoped to the run that produced them.
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

## Non-functional

- **R-080** The system should be local-only — no telemetry, no cloud calls, all data on the user's disk.
- **R-081** The system should be on-demand only — no daemon, no scheduled runs.
- **R-082** The system should be generic — no role, country, employer, or stack hard-coded in executable code; every preference lives in user data.
- **R-083** The system should never bypass anti-bot measures; sites that block automation are supported only via the manual-import provider type.
- **R-084** The system should log honestly: provider name, error class, status code on failure; counts and top score on success.
