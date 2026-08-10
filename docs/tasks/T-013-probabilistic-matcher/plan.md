# T-013 — Probabilistic same-ad matcher (dedupe phase 2)

Companion to T-012. The exact-key deduper (R-115) merges only what it can prove; what
remained after phase 1 were *title variants* of one ad — jobindex rendering the Workday
"Senior Software Engineer- (C#, APL) Valuation Product Area" as "Senior/**Lead** …" with
no location — plus every pair that differs by a token. Exact keys can never catch those,
and making the destructive deduper fuzzy was rejected in T-012: SimCorp posts *Senior*
and *Lead* Full-Stack as two real jobs with near-identical titles.

## Design: score likelihood, act non-destructively (R-117)

`Jobmatch/Deduplication/ProbabilisticMatcher.cs` — a Fellegi–Sunter-style scorer. Each
field contributes hand-set log₂-odds evidence; the sum becomes a probability and a band:

- **Blocking key:** canonical company equality (`Deduper.NormaliseCompany`). Different or
  missing company → `Distinct` outright; a missing company is *not* a wildcard.
- **Title** (`TitleSimilarity.cs`): token-set Jaccard over punctuation-split tokens
  (`c#`, `.net`, `c++` survive tokenisation), banded +10 (exact) down to −8; plus a
  **seniority signature** — {senior, lead, junior, student, …} tokens compared as sets,
  where a *subset* is no conflict ("Senior/Lead X" spans "Senior X"; unstated spans
  everything) but *divergent* signatures ("Senior X" vs "Lead X") cost −9, sinking any
  title similarity short of certainty. A **stack signature** works the same way over
  token *families* (−9 on divergence): ".Net udvikler" vs "Java udvikler" is two jobs
  however wordy the shared title, while C#/.NET/ASP.NET are one family so a cross-portal
  re-title between synonyms is not punished. Ambiguous English words ("go", "c") stay
  out of the family map — a false stack conflict silently unmerges a real duplicate.
- **Location:** the T-012 canonical place keys. Same place +3, different −7, either side
  missing 0 — the null-location wildcard lives here, where a mistake costs nothing.
- **Recency:** posted ≤14 days apart +1, >60 days apart −2, unknown 0.
- **Prior −4:** two same-company survivors are usually different roles.
- **Bands:** p ≥ 0.90 `SameAd`, p ≥ 0.30 `Possible`, else `Distinct`.
- **Same-portal cap:** a pair from one portal can reach `Possible` but never `SameAd` —
  the exact-key deduper already merged true same-portal duplicates by URL, so two
  distinct URLs on one source are almost always two reqs ("Senior X" and "X" on
  oracle-danskebank, found in the validation replay).

## Where the verdicts act

`BuildShortlist` (`SearchService.Ranking.cs`), not the deduper. Walking candidates in
score order: a `SameAd` match against an already-seated slot folds in as a **sighting**
(`ListingMatch.Sightings`, R-117) — freeing the slot for the next distinct role, applied
beyond the cut too, and recorded as a `duplicate_of_shortlisted` drop for the audit
trail (history-only; the removed view is retired). A `Possible` verdict is recorded on
`RunDetail.PossibleDuplicates` and costs nobody a slot. The matcher threads through
`JudgePlanner` so the LLM judge budget is spent on the grouped shortlist, not on
duplicates about to be absorbed.

GUI: shortlist cards render "Also seen on <portal>" links; the Duplicates view gains a
"possible duplicates" section (both locales).

## Validation on run 20260806-113247-dd3dc6

Replayed the run's 2 277 raw listings through the T-012 deduper and this grouping,
top-25 (scratch harness, not committed):

- Exactly two folds, both correct: the jobindex re-listing of SimCorp "Senior Software
  Engineer" (p=0.98) and the Senior/Lead APL pair (p=0.94) — the pair phase 1 left.
- Senior vs Lead Full-Stack, Wolt per-city postings, Workday per-site reqs: all kept.
- 13 `Possible` pairs, all genuine judgment calls — Workday same-title/other-city reqs
  at 0.33, the same-portal "Senior X"/"X" Danske Bank pair, and Sopra Steria's
  ".Net udvikler" vs "Fullstack **Java** udvikler" at 0.89: Danish filler tokens (til,
  afdeling, i, vækst) inflate Jaccard, and only the 0.90 threshold kept two different-
  stack roles apart — the finding that motivated the stack-signature guard above, which
  now sinks that pair to Distinct outright.

## Verification

18 matcher/similarity unit tests, 6 shortlist-grouping tests (fold, beyond-cut
absorption, possible-pair bookkeeping, seniority separation, per-portal sighting cap,
no-matcher passthrough), 6 GUI tests (card sightings link, dedupe-view possible
section), catalogs in en+da. Full suites green; `tsc -b` clean.

**Live runs** (scratch env per the verify skill, real provider fetches, runs
`20260810-080352` and `20260810-080801`): 2 145 fetched → 1 965 deduped → 25 slots;
the SimCorp jobindex re-listing folded with a working "Også set på SimCorp (Workday)"
link, the seeded Aug 6 run still rendered (no sightings row, old shape), and the
duplicates view showed the possible section in Danish with `dec()`-formatted
probability. The first live run caught two defects fixed on the spot: **a slot may
absorb at most one sighting per portal** — a null-location jobindex ad wildcards every
city and had claimed *both* Workday "Senior Software Engineer" reqs; one ad appears
once per portal, so the second claimant now demotes to a possible pair and keeps its
own candidacy — and the drop-context probability now formats invariantly ("0.98", not
the host culture's "0,98").
