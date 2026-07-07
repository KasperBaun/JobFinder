# todo

Current status of work on `jobfinder`.

## Backlog (next up)

- **Code-sign the Windows installer.** The unsigned electron-builder NSIS installer trips
  SmartScreen ("unknown publisher"). Needs a code-signing cert; wire it into `electron-builder.yml`
  (`win.signtool`/`signingHashAlgorithms`) and the CI publish step.
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
- **Application-status tracking + outcome calibration.** Extend marks beyond good/bad with an
  application status (applied / interview / rejected / no-response); later feed outcomes into the
  examples loop ("role type reached interview → strong-fit signal"). Requires a PRD scope note +
  new requirements when picked up. *(Concept: ai-job-search `job_search_tracker.csv`.)*
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

## In progress

_(none)_

## Shipped

See [`CHANGELOG.md`](CHANGELOG.md) for completed work.
