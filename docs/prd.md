# Product Requirements — `jobfinder`

## Vision

A personal job-search assistant. The user describes themselves once — what they're good at, where they live, what they don't want — and identifies the job providers they care about. On demand, they ask the system for a fresh shortlist of listings tailored to *them*. Over time, the user marks the listings that genuinely fit, and the system uses those signals to improve future rankings.

The system is private by design. Each user's profile, provider list, history, and feedback are scoped to that user alone — never shared, never aggregated, never sent anywhere the user did not explicitly direct.

## Actors

Actors in the PRD sense: the role that interacts with the system (the User), and the system itself described through the responsibilities it owns and the qualities it commits to. The user's goals below map to use cases; the system's responsibilities are the system's commitments while serving them.

### User

A working professional looking for their next role. They have a clear sense of their stack, target seniority, dealbreakers, and the platforms worth checking — but they don't want to scroll all of them every week.

The user wants to:

- **Remember themselves once.** Capture their skillset, preferences, and disqualifiers in a place they can edit and revise, and never re-enter that information for future searches.
- **Pick the providers that matter.** Configure a curated set of job providers and toggle them on or off without re-authoring anything else.
- **Receive a short, ranked answer.** Initiate a search and see a shortlist of openings that actually fit, with honest reasoning per match.
- **See what they have configured.** Inspect the providers currently in play and the search criteria currently driving rankings, so changes are visible and intentional.
- **Look back at past searches.** Review previous runs with timestamp, how many listings came back, and how many of those they later marked as a good match.
- **Teach the system over time.** Mark listings as good or bad matches so future rankings surface more of the former and less of the latter.
- **Trust the system.** Be confident the system acts only when initiated, communicates only with providers the user explicitly configured, and respects the boundaries those providers set.

### System (`jobfinder`)

The system fulfils the user's goals through the following responsibilities and qualities.

**Responsibilities**

- **Maintain the user's profile.** Persist the user's skillset, preferences, and disqualifiers between sessions and present a single authoritative view of them.
- **Maintain the user's provider list.** Hold the user's configured providers, each with an explicit enabled / disabled state and a declared access mode.
- **Aggregate listings on request.** Retrieve listings from each enabled provider, merge them into a deduplicated set, and preserve the per-provider source results for later inspection.
- **Rank against the profile.** Score each listing against the user's profile using user-controlled weights and surface a top-N shortlist with explicit, evidence-based reasoning per match.
- **Honour user feedback.** Capture the user's "good match" signals against listings and runs, and make those signals available for ranking improvements over time.
- **Preserve a search history.** Remember every run — when it took place, which providers were involved, how many listings were considered, how many made the shortlist, and how many the user later marked.
- **Surface configured state.** Make the active profile, the active provider list, and the search history visible to the user without further setup.

**Qualities**

- **User-isolated.** Treats each user's profile, providers, history, and feedback as private and scoped to that user; never mixes data across users.
- **On-demand.** Acts only when the user initiates an action; no scheduling, polling, or background activity.
- **Generic.** Carries no domain-specific bias — no preferred role, country, employer, or technology. All preferences originate from the user.
- **Honest.** Reports failures with cause and context, attributes positive signals only when supported by evidence, and never bypasses a provider's anti-automation measures.
- **Resilient to partial failure.** A single provider's failure does not invalidate the rest of a run; the system continues with the providers it can reach and reports the rest.
- **Deterministic given fixed inputs.** The same profile, providers, and weights produce the same shortlist for the same listings.
- **Locally controlled.** The user's data remains under the user's own control; the system communicates only with providers the user has explicitly configured.

## Product principles

- **Single source of truth per concept.** Exactly one active profile, one active provider list, one history. No shadow copies, no parallel views that can drift.
- **One model, many doorways.** Every capability is reachable regardless of how the user chooses to interact with the system; no operation is exclusive to a particular interaction style.
- **YAGNI, ruthlessly.** No multi-user accounts, no scheduling, no cover-letter generation, no salary normalisation. The system surfaces ranked listings.
- **Fail loud, not weird.** Broken providers, missing configuration, unreachable endpoints — surface the problem with context; never paper over it.

## Scope summary

**In scope**

- Profile authoring and refinement.
- Provider configuration with multiple access modes (automated and manual).
- Aggregation, deduplication, and ranking of listings against the profile.
- Multiple interaction styles drawing on the same set of capabilities.
- Per-run history and user-applied "good match" feedback.

**Out of scope**

- Cover-letter or application generation.
- Salary normalisation or estimation.
- Cross-user features, sharing, or external synchronisation.
- Scheduled or background runs.
- General-purpose web crawling.

## See also

- [`requirements.md`](./requirements.md) — one-line requirements (the contract).
- [`mwt-tool-analysis.md`](./mwt-tool-analysis.md) — architectural reference for implementers.
