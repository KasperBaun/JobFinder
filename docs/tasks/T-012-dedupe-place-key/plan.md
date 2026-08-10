# T-012 — Cross-portal dedupe, phase 1: canonical place keys

Follow-up to the T-011 review's biggest find: cross-portal duplicates reached the
Top-jobs shortlist of run `20260806-113247-dd3dc6` — "Senior Software Engineer C#/.net"
at #1 *and* #2 (oracle vs jobindex), the SimCorp .Net/Angular role three times in the
top 20. See `docs/tasks/T-011-results-polish/plan.md` ("Left out deliberately").

## Diagnosis

The deduper (`Jobmatch/Deduplication/Deduper.cs`) is a single pass with two **exact**
keys — normalised URL, and `title|company|location` compared ordinally. Against the
run's raw listings, every surviving top-20 pair failed on spelling conventions, not
substance:

1. **City-name language** (the dominant killer): `Copenhagen V, Denmark` vs
   `København` — string normalisation can never make them meet, and the deduper never
   consulted the `Gazetteer` the radius filter already uses.
2. **HTML-entity depth**: `&amp;amp;` vs `&amp;` vs `&` in a title splits the key.
3. **Key shadowing**: a listing absorbed by URL never registered its own
   title/company/location key, so its variant spelling stayed invisible to a third
   portal's copy.

## Decision: keys stay exact and binary

The merge is destructive (the duplicate listing is dropped), so precision beats
recall here. No fuzzy title matching: SimCorp posts *Senior* and *Lead* Full-Stack as
two real jobs with near-identical titles — a similarity threshold would collapse them.
What phase 1 does instead is make the exact key *canonical*:

- **Location resolves through the bundled gazetteer** (`Deduper.NormaliseLocation`):
  after the existing reduction (remote-suffix, first comma segment, district letter),
  the string resolves via `Gazetteer.ResolveSites` and the key becomes the sorted set
  of canonical places (`copenhagen #dk`). A multi-site listing (`Noida / Hyderabad`)
  matches on its site *set*, never on whichever site is listed first — first-site
  keying proved order-dependent and over-eager on the real run. Unresolved strings
  fall back to plain normalisation; the `#` suffix keeps the two key spaces disjoint.
- **Entity decoding inside `Normalise`**, looped to a fixed point so encoding depth
  never splits a key (old runs predate the T-011 `ListingTextDecoder` chokepoint).
- **Absorbed listings register their other key** (URL-absorbed → title key, and vice
  versa), closing the phantom-third-copy gap.

`SearchService` passes its gazetteer (or the bundled one) at the one production call
site. Requirement: **R-115**.

## Verification

- 14 new tests (`DeduperGazetteerTests.cs`): the run's actual Danske Bank pair, EN/DA
  city equivalence with and without gazetteer, Århus/Aarhus folding, entity-depth
  theory, both key-registration chains, multi-site order-insensitivity, multi-site ≠
  single-site, Senior-vs-Lead pinned as *distinct*, bundled-gazetteer real-world
  spellings, unresolved fallback. Full backend suite: 831 green.
- Replayed the deduper over the seeded run's 2 277 raw listings: 2 089 → 2 081, and
  the eight new merges are exactly the cross-portal pairs the review named (Danske
  Bank, SimCorp ×4, Milestone ×3) — nothing speculative. Every remaining
  same-title+company group is legitimately split by site (Wolt per-city postings,
  Maersk per-warehouse).

## Phase 2 (backlog, not this task)

Title *variants* of one ad ("Senior…" vs "Senior/Lead…", jobindex's rewritten titles)
still survive, and must: the destructive pass cannot gamble on them. The remaining
work is non-destructive — group same-company similar-title entries (null location as
wildcard) into one shortlist slot at `BuildShortlist` time, and add sightings to
`ListingMatch` so a Top-jobs card can say "also seen on jobindex". Tracked in
`todo.md`.
