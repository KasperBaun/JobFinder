# T-011 — Run-results page polish (R-085 follow-up)

The seven R-085 commits consolidated the run-detail page (one toolbar, view menu,
collapsed filters, histogram, pagination), but the page still didn't feel finished. A
UI/UX review agent drove the live app — Vite + API seeded with the real run
`20260806-113247-dd3dc6` (25 top jobs, 2 089 rated, 51 providers, 118 duplicates,
2 064 removed) — interacted with every control, and screenshotted every state. Its
verdict: **~80–85 % there; structure succeeded, the remaining distance was meaning and
noise**, concentrated in the all-rated table. All ten findings were implemented on
`feat/results-polish`, one commit per finding.

## What the review found working

- The one-row toolbar reads as one system: search → dark view-menu pill → quiet filter
  pills, prominence decreasing left to right.
- The view menu is genuinely clear (per-view counts, primary/audit divider, active item
  weighted).
- Active-filter feedback is strong: filled pills with counts, the score range inline on
  its trigger, "Nulstil filtre" only when non-default, "259 af 2.089 job".
- Filter popovers are high quality — portal search + counts, the histogram slider
  operable by keyboard in 0,01 steps.
- The sticky sort bar earns its stickiness; the empty state recovers inline.
- Performance is a non-issue: sorting 2 089 rows 39 ms, search 45 ms, 300-row page
  render 114 ms.
- Accessibility basics in place: focus rings, Escape everywhere, `aria-sort`,
  `aria-expanded`, localized aria-labels.

## Findings and how each was resolved

| id | sev | Problem observed | Resolution |
|----|-----|------------------|------------|
| F1 | high | The score bar was a full-width composition bar: a 0,02 job showed the same-length bar as a 0,94 job — the one graphic in the column contradicted the number beside it | The bar's length now encodes the score; the component segments split that length (`BreakdownBar` gains a `score` prop, fill wrapper + track) |
| F2 | high | "Din vurdering": a two-line outline button stacked on a native select, repeated 100×, the tallest and loudest element on the page | Compact ✓/✕ toggle glyphs in the table, full labels on cards; an unset status select renders as quiet ghost text; row height is content-driven again |
| F3 | med | One cycling mark button: reaching "bad" persisted a wrong "good" first — a false training signal for the ranker | Good and bad are two explicit `aria-pressed` toggles; re-click clears; no intermediate state is ever persisted (R-113) |
| F4 | med | Undecoded entities in titles ("Work &amp;amp; Security") | Every fetched listing now passes one decoding chokepoint after `FetchAsync` (`ListingTextDecoder`), so titles, company and location are decoded whatever the source; markdown/PDF outputs inherit the fix. Existing runs keep recorded text |
| F5 | med | Three reset treatments (toolbar "Nulstil filtre", bar "Nulstil sortering", empty-state bare "Nulstil") and native selects clashing with the pill family | Empty state reuses the "Nulstil filtre" wording; `select--inline` and the direction toggle take the pill radius. The R-112 words-based sort select stays — headers still scroll out of reach sideways |
| F6 | med | Page change kept the scroll offset (scrollY 4 000 → 4 000), silently swapping rows underneath the reader | The table re-anchors at its top on page change (and only on page change) |
| F7 | med | At ~1000 px the search collapsed to 92 px with a truncated placeholder; a lone stat wrapped under the summary card leaving half of it empty | The search never drops below its placeholder width (filter pills wrap instead); the stat metrics are a re-flowing grid |
| F8 | low | Portal badges in the Jobkilde column read as interactive chips — false affordance ×100 when filtered to one portal | Plain quiet mono label |
| F9 | low | View menu mixed "Topjob" with "alle hentede"; Danish said "1 andre steder"; duplicates showed bare titles with no portals | Sentence case across all five views; singular/plural branch in both catalogs; each sighting (kept + merged) now names its source — the point of auditing a merge |
| F10 | low | The removed view rendered all 2 064 rows unpaginated | Reuses the pager + page-size menu with local state, clamped and re-anchored like the longlist |

## Left out deliberately

The review's biggest *product* finding is not a page issue: the Top-jobs shortlist
contains obvious cross-portal duplicates ("Senior Software Engineer C#/.net" at #1 and
#2 via oracle and jobindex; a SimCorp role three times in the top 20). Same-employer
listings that dedupe misses because title/URL differ per portal hurt the top-10 metric
more than any styling. Recorded in `todo.md` as backlog; not addressed here.

## Verification

Frontend: Vitest (incl. new `MarkButton.test.tsx` pinning the two-toggle semantics),
`tsc -b` (catalog parity), ESLint. Backend: `ListingTextDecoderTests`. Runtime: the
review setup replayed against the same seeded run, per finding, in both locales.
