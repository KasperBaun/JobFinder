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

`ProbabilisticDeduper` (`Deduplication/ProbabilisticDeduper.cs`), run on the exact-key
deduper's survivors **before ranking** — so no duplicate reaches the scored list, the
LLM judge budget, or the shortlist. (The first iteration grouped at shortlist time,
defending only the top-N; product direction was explicit that duplicates are removed
during dedupe, not merely kept off the shortlist, and the persisted raw sections plus
the duplicates audit view make the destructive step recoverable and inspectable.)

Listings are processed most-informative first (located beats location-less, fuller text
beats a stub) so the copy the ranker can do the most with survives; `SameAd` absorbs
into the canonical as a **sighting** (`ListingMatch.Sightings`, probability kept), a
`Possible` verdict never merges and is recorded on `RunDetail.PossibleDuplicates` only
at p ≥ 0.5 — below that the band is dominated by same-title/other-city postings, real
distinct roles that would drown the audit view. Two rules bound the destructive step:
same-portal pairs never reach SameAd, and a canonical absorbs at most one listing per
portal (a second same-portal claimant is that portal's other req and survives).

GUI: shortlist cards render "Also seen on <portal>" links; the Duplicates view lists
probabilistic merge groups beside the exact ones plus the "possible duplicates" section
(both locales).

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

**Live runs** (scratch env per the verify skill, real provider fetches). Runs
`20260810-080352`/`-080801` exercised the original shortlist-time grouping and caught
two defects fixed on the spot: the one-sighting-per-portal rule (a null-location
jobindex ad had claimed *both* Workday "Senior Software Engineer" reqs) and invariant
formatting of the drop-context probability. Runs `20260810-084837`/`-085714` exercised
the dedupe-phase pass with a full manual audit of every slot, sighting, possible pair
and a same-company title-overlap sweep of the ranked list. The audit drove two more
changes: **location compatibility** — differing keys were −7 even when one side merely
spoke coarser ("Denmark" vs "København V"; "Indien, Litauen, Aarhus" vs "Aarhus C";
"Nordhavn, København Ø" vs "København Ø"), so resolved sites within 30 km, or a
country-only claim covering the other side's country, are now neutral instead of
penalised — and the **possible floor moved 0.5 → 0.6** because recency agreement had
lifted same-title/other-city postings to exactly 0.5, flooding the audit list (354
rows → 146, now sorted strongest-first). After both: cross-portal same-title
duplicates in the *whole ranked list* fell 10 → 5, and each survivor is a deliberate
keep — contradictory locations (jobindex's "Frankrig" vs Copenhagen, Manila vs
København, Suzhou vs Bjerringbro) or unresolvable text ("Udlandet",
"Headquarters (IT)") where merging would be a guess.

A third iteration (runs `20260810-090726`/`-091401`) added the remaining evidence
fields. **Body text** (`DescriptionSimilarity`): word-shingle containment, so a
portal's excerpt registers against the full ad; near-copy +6, overlap +2, substantial
disjoint −3, computed lazily only for pairs the other fields put near a boundary — and
*suppressed entirely when two resolved places disagree*, because one employer's reqs
share template text (SimCorp Manila must not merge into København on boilerplate).
This settled both unresolvable-location survivors live: Saxo's "Headquarters (IT)" and
SimCorp's "Udlandet" ads merged on their bodies, taking the ranked-list residue 5 → 3,
all three resolved contradictions. **Company drift**: blocking widened to the first
company token with a token-*subset* gate ("Danske Bank" ⊂ "Danske Bank Group" compares
at −1; "Danske Bank" vs "Danske Spil" never does) — the run-6 audit found zero false
merges from it. **Audit ordering**: possible pairs carry a `samePortal` flag and sort
cross-portal-first then probability; the duplicates view previews the strongest 30
with a show-all expander.
