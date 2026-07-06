# todo

Current status of work on `jobfinder`.

## Backlog (next up)

- **[TOP PRIORITY] Capture a "why" reason when rating a match, and feed it back to the LLM.**
  When the user marks a listing "good"/"bad" they should be able to attach a short free-form
  reason ("I'm not a student", "wrong stack", "too junior"), and that reason must reach the
  judge on the *next* run so it stops repeating the mistake. Concrete miss this round: an
  **"AI Engineer - Student"** posting scored **0.81** — a horrible match, because the candidate
  is not a student, but nothing tells the model that.
  Two-part change:
  1. **Store the reason.** Extend `MarksService.Set` / `marks.json` beyond the bare
     `"good"`/`"bad"` string to carry an optional `reason` (per run/listing), and surface it in
     the UI mark control (`MarksHandler`, frontend mark button).
  2. **Feed it into the judge.** Route captured reasons into the LLM judge prompt for the next
     search — e.g. promote a marked-bad listing + reason into a `disliked` example, or append the
     reasons to the few-shot block. Note: `ExamplesLoader.ToFewShotPrompt` currently sends only
     frontmatter and *drops the free-form body*, so the existing "why" prose in `examples/*.md`
     never reaches the model either — fixing that is part of closing this loop.
  Validate against `examples/` per [[feedback_validate_against_examples]]: success = the
  student/junior/wrong-stack archetypes fall out of the top-10 on the following run.
- **Code-sign the Windows installer.** Unsigned `Setup.exe`/portable exe trips SmartScreen
  ("unknown publisher"). Needs a code-signing cert; wire it into the CI publish step.
- **Switch data location / user after setup.** First-run setup is one-time; add a Settings action
  to re-run it or point at a different data folder (updates `bootstrap.json`).
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
- **Source-specific remote-mode extraction (`ApiAdapter` /
  `HrManagerAdapter`).** 5 of the current top-10 still tag `Unknown` —
  the frontend hides the badge but the data is recoverable from source.
  SmartRecruiters' JSON `customField` array commonly carries "Workplace
  policy / Hybrid"; HR-Manager's JSON-LD often has `workLocation` /
  `employmentType`. Per-adapter: pull the structured value first, fall
  through to `BaseAdapter.InferRemoteMode` only when those fields are
  silent.
- **LLM judging speed-up — system-prompt KV caching.** Current run is
  ~19 sec/listing on CPU → 50 listings ≈ 16 min. The system prompt is
  identical across every call; only the user prompt varies. Pre-tokenise
  the system prompt into a "warm" KV state once and rewind to it between
  calls instead of `MemoryClear` (see `LLamaContext.SaveState` /
  `LoadState` in LLamaSharp 0.27). Target ~5-10× speedup. Lower-hanging
  follow-ups: GPU offload (already a documented `llm.gpu_layer_count`
  knob — needs a `LLamaSharp.Backend.Cuda12` / `.Vulkan` swap in
  `Directory.Packages.props`); lower `llm.top_n` 50 → 25.

## In progress

_(none)_

## Shipped

See [`CHANGELOG.md`](CHANGELOG.md) for completed work.
