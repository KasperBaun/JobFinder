# todo

Current status of work on `jobfinder`.

## Backlog (next up)

- **Radius-filter residuals (R-105), each measured and deliberately left.** From the
  2026-08-05 verification pass over a real 2 330-listing corpus; frequency in that
  corpus in brackets. *(a)* A bare four-digit token in a string where nothing else
  resolves is still read as a Danish postcode, so a foreign address whose town is
  below the gazetteer's 100k floor can land in Denmark, and it fails **open** when the
  number happens to be a Zealand postcode: the one corpus case (`USCUB01 - Curtis Bay -
  7550 Perryman Court`) still dropped only because 7550 is Sørvad, while a house number
  of 2750 would have read as Ballerup, 40 km from home [1 listing] — requiring positive Danish evidence instead would lose real drops like
  "2670 Greve Strand", so this needs a better signal, not a tighter rule.
  *(b)* `Gazetteer.RemoteTokens` matches "global" as a substring of the whole
  location, so "Global HQ, Aarhus" exempts itself from the filter [0] — the list
  mirrors `Ranker.Location.cs:97`, so change both or neither. *(c)* Gazetteer long
  tail: Vietnamese districts, Canadian street addresses, a typo'd "United States of
  Americas", and "København C" (not a real postal district) resolve to nothing or to
  the country [~10 listings, all fail-open]. *(d)* A fractional radius renders as
  "max 0 km" because the drop args are ints; the GUI only produces whole numbers.
  *(e)* `RemoteMode` is computed before body enrichment appends the full page text,
  so classification sees a partial description [measured delta after R-110: 58 vs 60
  listings].
  *(f)* If the job-page fetch fails, a location recovered from a truncated cell degrades
  to the single named site and is hard-dropped on information we know was partial
  [0 of 50 failed on a live re-fetch]. Closing it properly means teaching the filter
  about incompleteness rather than stripping the marker — deliberately not coupled.
  *(h)* HR-Manager ships no structured work arrangement — verified across four customers
  and twelve ad pages on 2026-08-06 — so its listings stay inference-only (R-110). One
  customer puts "Up to 40% remote" in the free-text WorkHours field; parsing that is the
  substring test R-110 removed.
  *(g)* The own-country area waiver covers **every** region of the home country, so a
  "Region Midtjylland" job is kept for a Copenhagen user [2 listings]. Chosen because
  the opposite error hides jobs in the region the user actually lives in. A
  nearest-centroid (Voronoi) test would waive only the region containing the home and
  restore the rest — worth doing if region-only locations ever become common.

- **Localize `top-jobs.md` and the verification report.** Both are written server-side,
  so they need a C#-side message table rather than the frontend catalog. Only worth doing
  if users actually open the generated markdown.

- **Reconsider re-enabling `jobindex-rss-softwareudvikler`** (id 14,
  currently user-disabled) — still the single widest DK net we have.
- **Recruit IT html scrape — re-verify endpoint, then enable.** `HtmlAdapter` now
  resolves a `:scope` `link_selector` to the matched list element itself (done
  2026-07-06, covered by a test — same pattern used by the new cBrain/Nine
  sources), so the adapter side is handled. Remaining: confirm `recruit-it.dk`
  still serves the expected server-rendered markup, then flip `enabled: true`.
- **Recruit IT location parsing.** Location renders as a plain text node
  next to an icon with no wrapper class; `location_selector` is intentionally
  omitted and listings will have null location until the markup changes.
- **`jobsearch-dk` company/location parser.** Items expose only `title` /
  `description` / `link` — no `pubDate`, no structured company/location.
  Add a parser that extracts company/location from the title or URL slug
  `/{role}/{city}/{id}`.
- **Remove migration shim.** `PortalsMigrationShim.RunIfNeeded` runs on every
  Gui startup. After all known users have run the new build at least once,
  delete the shim, its tests, and the YAML loader's only remaining caller path.
- **"New since last run" flag.** Mark listings in results/longlist that never appeared in any
  prior history run (compare canonical dedupe keys against `history/*.json`); badge + filter in
  `LonglistTable`. *(Concept from MadsLorentzen/ai-job-search `seen_jobs.json` cross-run dedupe.)*
- **Career-goals/motivation signal for the judge.** Add a free-text "career goals / what
  energizes & drains me" section to the skillset (form + `skillset.md` + `SkillsetParser`) and
  include it in the `LlmJudge` prompt. Complements the top-priority mark-reason item. Add an
  R-NNN to `docs/requirements.md` when implemented. *(Concept: ai-job-search's career-alignment
  scoring dimension.)*
- **Skill-gap heatmap (local-only).** Aggregate skills required by ranked/dropped listings but
  absent from the skillset, weighted by `(1 − fit_score) × frequency`; render as a prioritized
  table on the History run view. No web-searched learning resources (local-only constraint).
  Scope extension — add a requirement when implemented. *(Concept: ai-job-search `/upskill`.)*
- **Evaluate Jobdanmark.dk.** Run the T-007 portal playbook (`docs/tasks/T-007/`) — a portal
  ai-job-search supports that we never evaluated. (LinkedIn `jobs-guest` public endpoints were
  considered and rejected: explicitly against LinkedIn ToS; LinkedIn stays `manual`.)
- **LLM judging speed-up — system-prompt KV caching.** Current run is
  ~19 sec/listing on CPU → 50 listings ≈ 16 min. The system prompt is
  identical across every call; only the user prompt varies. Pre-tokenise
  the system prompt into a "warm" KV state once and rewind to it between
  calls instead of `MemoryClear` (see `LLamaContext.SaveState` /
  `LoadState` in LLamaSharp 0.27). Target ~5-10× speedup. Lower-hanging
  follow-ups: GPU offload (already a documented `llm.gpu_layer_count`
  knob — needs a `LLamaSharp.Backend.Cuda12` / `.Vulkan` swap in
  `Directory.Packages.props`); lower `llm.top_n` 50 → 25.

## Postponed

- **Code-sign the Windows installer.** Deferred — too much setup (cert acquisition, CI
  secrets, Windows-only testing) for no valuable result on a personal/single-user tool right
  now. Revisit if the app is distributed to strangers. Options if resumed: self-signed cert
  (free; only removes the warning on machines that trust it once — fine if distribution is
  just you); SignPath Foundation (free, but repo must be public + OSS-licensed + approved);
  Certum Open Source (~€30/yr, EU-individual friendly); Azure Artifact Signing (~$10/mo);
  EV cert (~$250/yr — the only one that suppresses the SmartScreen popup instantly). Plain OV
  `.pfx` signing is no longer issued (2023 hardware-key mandate). Wiring points when resumed:
  `win.*` in `src/desktop/electron-builder.yml` (the TODO comment at line ~26) + the
  `npm --prefix src/desktop run dist` step in `.github/workflows/release.yml`.

## In progress

_(none)_

## Shipped

See [`CHANGELOG.md`](CHANGELOG.md) for completed work.
