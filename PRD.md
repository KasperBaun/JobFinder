# PRD — `jobmatch`

## 1. Summary

`jobmatch` is a local, configurable job-matching tool. The user writes a **skillset profile** in one file, lists the **job portals** they want to pull from in another, and runs the tool. It fetches listings, deduplicates across portals, ranks them against the profile, and writes a top-N shortlist.

Generic by design: swap the skillset, swap the portal list, re-run. Nothing in the core prefers any specific role, stack, or country. Every piece of personal context lives in user-editable config files; the tool itself does not.

---

## 2. Personas

Concrete archetypes. All three must be serviceable with only config changes — no code edits.

### 2.1 Mikkel — .NET/Azure consultant in Copenhagen
Mid-to-senior backend consultant, seven years of experience. Works in C#, .NET, Azure, SQL Server. Open to hybrid in Greater Copenhagen. Wants to avoid pure-frontend postings and unpaid engagements. Speaks Danish and English. Tired of scrolling LinkedIn; wants to run one command Sunday evening and see the week's relevant openings ranked.

### 2.2 Lena — React/Rust engineer in Berlin
Mid-level full-stack engineer, four years' experience. TypeScript, React, Next.js on the frontend; moving into Rust for performance-sensitive work. Open to remote-in-EU. Wants fintech or developer-tools domains. Disqualifies contract-to-hire bait-and-switch listings and any role requiring relocation to US offices.

### 2.3 Rui — Data scientist in Lisbon
Senior data scientist, eight years' experience. Python, PyTorch, MLOps, Snowflake. Remote-only, EU timezone. Focus on healthcare and climate domains. Disqualifies quant-finance trading roles and commission-based recruiting shops.

---

## 3. User stories

Format: _As a <user>, I want <capability>, so that <benefit>._

- **US-001 — Define skillset once.** As a user, I want to describe my profile (experience, location, primary stack, secondary stack, domains, disqualifiers) in a single file I can version and edit by hand, so that I don't re-enter the same information every run.
- **US-002 — Configure portals once.** As a user, I want to list the job portals I care about and how each one is accessed (public API, RSS feed, manual export) in one config file, so that I can enable, disable, or add portals without touching code.
- **US-003 — Fetch and rank in one command.** As a user, I want a single command that pulls from all enabled portals, deduplicates the results, scores them against my skillset, and produces a ranked top-N list, so that I can triage openings in a few minutes instead of an evening.
- **US-004 — Verify the setup before relying on it.** As a user, I want a command that checks my skillset, portal config, and ranking weights are valid and reachable, so that I find a typo or broken URL before I need the results.
- **US-005 — Swap profile without code changes.** As a user, I want to swap my skillset file (or keep several side by side) and re-run, so that I can test "what if I targeted staff roles" or "what if I dropped Azure" without editing the tool.
- **US-006 — Include portals that can't be automated.** As a user, I want to drop an export file (CSV or JSON) from a portal that blocks automation into a known location and have those listings included in the ranked output, so that I'm not shut out of sites that don't expose an API.
- **US-007 — One dead portal doesn't kill the run.** As a user, I want an adapter failure (timeout, rate limit, server error) on one portal to warn me and move on, so that a down portal doesn't waste the fetches from the working ones.
- **US-008 — Hard-disqualify listings.** As a user, I want to list deal-breaker keywords and have matching listings score zero, so that junk doesn't pollute the top of my shortlist.
- **US-009 — Honest reasoning per match.** As a user, I want each top match to show which signals contributed (primary-stack hits, seniority match, remote match, freshness) and a short human-readable note, so that I can trust the ranking and spot when the tool is wrong.
- **US-010 — Control the shortlist size.** As a user, I want to set how many matches surface (default 10) and the minimum score to include, so that a thin week produces a small shortlist instead of ten weak matches.

---

## 4. Functional requirements

### 4.1 Skillset

- **FR-001** The tool must read a skillset from a single user-authored file at a known location under `config/`.
- **FR-002** The skillset must cover: name, location, years of experience, target roles, remote preference, seniority, primary stack, secondary stack, domains, disqualifiers, spoken languages, acceptable employment types.
- **FR-003** The tool must provide a command that generates a fresh skillset interactively (a short series of prompts) or by reading a CV file or URL the user points at.
- **FR-004** The skillset command must refuse to silently overwrite an existing skillset. It must diff and ask.
- **FR-005** A committed example skillset ships in the repo so new users see the shape.

### 4.2 Portals

- **FR-006** The tool must read a list of portals from a single user-authored file at a known location under `config/`.
- **FR-007** Each portal entry must declare its access type: public API, RSS/Atom feed, browser-rendered HTML, or manual import.
- **FR-008** Portals must have an `enabled` flag so users can toggle them without removing them.
- **FR-009** Only configured portals may be fetched. The tool is not a general-purpose web crawler.
- **FR-010** A committed example portal list ships in the repo with at least one working public-API portal.

### 4.3 Listings

- **FR-011** The tool must fetch listings from every enabled portal when the listings command runs.
- **FR-012** The tool must persist raw per-portal results for debugging and reproducibility.
- **FR-013** The tool must deduplicate listings across portals, preferring URL as the primary key and falling back to a normalized title-plus-company match.
- **FR-014** Deduplicated listings must be persisted as a single merged file.
- **FR-015** The tool must rank listings using the algorithm in §6 and persist both a full ranked file and a human-readable top-N summary.

### 4.4 Verification

- **FR-016** The tool must provide a verification command that runs the following checks and reports pass / warn / fail per check, without crashing on any individual failure:
  1. Required config files exist.
  2. Skillset parses and has a non-empty primary stack.
  3. Portal config parses and each enabled portal has the fields its type requires.
  4. Ranking weights sum to 1.0 ± 0.01 and top-N ≥ 1.
  5. Enabled API and RSS portals respond to a minimal test request (short timeout).
  6. Enabled manual portals have at least one matching import file on disk.
  7. Enabled HTML portals have the optional browser-automation component available, or surface a clear install instruction.
- **FR-017** The verification command writes a report file and prints an equivalent summary to stdout.
- **FR-018** The verification command exits non-zero if any check fails.

### 4.5 Graceful degradation

- **FR-019** A failure in a single portal adapter (network error, parse error, unexpected schema, timeout) must not crash the listings command. It must log a warning naming the portal and the failure mode, skip that portal, and continue with the others.

### 4.6 Slash commands

The tool exposes three Claude Code slash commands. Each is a thin wrapper around a corresponding CLI subcommand so identical behaviour is available from the terminal.

- **FR-020** `/generate-skillset` — interactive or URL/path-seeded skillset authoring (US-001, US-005).
- **FR-021** `/generate-job-listings` — fetch, dedupe, rank, write outputs (US-003, US-007, US-010).
- **FR-022** `/verify-config` — run all verification checks (US-004).

---

## 5. User-facing artifacts

### 5.1 `config/skillset.md`
A markdown file: a YAML frontmatter block (identity, role targets, preferences) followed by markdown sections — `## Primary stack`, `## Secondary stack`, `## Domains`, `## Disqualifiers` — each a bullet list of keywords. Authored by the user, gitignored. The committed `config/skillset.example.md` shows the shape with generic placeholder content.

### 5.2 `config/portals.yml`
A YAML file with a top-level `portals:` list. Each entry has a unique `name`, a `type` from {api, rss, html, manual}, an `enabled` flag, and type-specific fields (endpoint URLs, query parameters, response-field mappings, rate-limit caps, notes). Authored by the user, gitignored. The committed `config/portals.example.yml` shows the shape with at least one working public-API portal.

### 5.3 `config/ranking.yml`
A YAML file that controls the ranking algorithm's behaviour: weights per signal (must sum to 1.0), the disqualifier penalty, the top-N cutoff, the freshness half-life in days, and the minimum score to include in the shortlist. Committed with sensible defaults because it contains no personal content.

### 5.4 Outputs (under `data/`, gitignored)
- `raw/<portal>-<YYYYMMDD>.json` — per-portal raw fetches
- `all_listings.json` — merged, deduplicated
- `ranked_listings.json` — full ranked output with match reasoning
- `top_jobs.md` — human-readable top-N with titles, companies, scores, one-line reasoning, links
- `verification-report.md` — last verification run

### 5.5 Manual imports (under `data/imports/`, gitignored)
Users drop `<portal-name>-*.csv` or `<portal-name>-*.json` files here for portals configured with type `manual`. Expected fields: `title`, `company`, `location`, `url`, `description`, `posted_at`.

---

## 6. Ranking behaviour

Every listing receives a score between 0.0 and 1.0.

**Signals that contribute positively** (each with a user-configurable weight):
- **Primary stack** — fraction of the user's must-have keywords found in the listing's title or description.
- **Secondary stack** — same, for nice-to-have keywords.
- **Seniority** — full credit for an exact match with the user's declared seniority, partial for an adjacent level, zero otherwise.
- **Location / remote** — full credit when the listing's location matches the user's location, or when remote availability matches the user's remote preference.
- **Domain** — fraction of the user's declared domains found in the listing.
- **Freshness** — newer listings score higher; older ones decay exponentially with a user-configurable half-life.

**Signals that zero the score:**
- **Disqualifiers** — if any user-declared disqualifier keyword appears in the listing, the score is multiplied by the disqualifier penalty (default 0.0).

**Shortlist filters** (applied after scoring):
- Any listing scoring below the minimum-score threshold is dropped.
- The top N are surfaced, in descending score order.

**Match reasoning.** Every surfaced match carries an honest breakdown: which primary/secondary keywords hit, whether seniority matched, whether location/remote matched, which domains hit, which disqualifiers (if any) fired, plus one or two sentences summarising the above. The tool must not invent reasoning. If a signal is unknown (e.g. location missing from the source), the reasoning says so.

Matching is case-insensitive and whole-word. Both the listing's title and description participate in keyword matching.

---

## 7. Non-functional requirements

- **NFR-001 — Graceful degradation.** One portal failure must not crash the run (mirrors FR-019).
- **NFR-002 — No anti-scraping.** The tool must not bypass anti-bot protections, solve CAPTCHAs, rotate sessions, or simulate humans. Portals that block automation are supported only via the `manual` type.
- **NFR-003 — On-demand only.** No background daemon, no scheduled runs. A single command per operation.
- **NFR-004 — Local-only.** No cloud services, no telemetry, no external state. All data lives on the user's disk.
- **NFR-005 — Generic.** Every persona in §2 must be serviceable with only config changes. No identifier, keyword, country, or portal may be hard-coded in executable code.
- **NFR-006 — Honest logging.** Failures surface with enough context to debug (portal name, error class, HTTP status where applicable). Successes summarise: fetched count, post-dedupe count, scored count, top score, any adapters that failed.

---

## 8. Non-goals

- Not a general-purpose web crawler. Only configured portals.
- Not an application assistant. Output is a shortlist, not cover letters.
- Not a scheduler. Runs on demand.
- Not an anti-scraping toolkit.

---

## 9. Acceptance criteria

- A user with zero knowledge of the code can clone the repo, run `/generate-skillset`, then `/verify-config`, then `/generate-job-listings`, and get a populated `data/top_jobs.md` — without editing source code.
- Swapping `config/skillset.md` and re-running produces a different ranking and nothing else changes.
- Disabling a portal in `config/portals.yml` removes it from the next run with no code change.
- An adapter failure (e.g. portal returns 503) produces a warning, not a crash, and other portals still complete.
- The verification command catches the three most common setup mistakes: missing skillset, malformed portal config, weights not summing to 1.

---

## 10. Out of scope for v1

- Cover-letter or application generation.
- Salary normalisation or estimation.
- Cross-run state (e.g. "hide listings I've already seen"). Candidate for v2.
- Multi-user or shared config.
- Scheduled runs.
