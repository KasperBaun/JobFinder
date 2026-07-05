# todo

Current status of work on `jobfinder`.

## Backlog (next up)

- **Code-sign the Windows installer.** Unsigned `Setup.exe`/portable exe trips SmartScreen
  ("unknown publisher"). Needs a code-signing cert; wire it into the CI publish step.
- **Switch data location / user after setup.** First-run setup is one-time; add a Settings action
  to re-run it or point at a different data folder (updates `bootstrap.json`).
- **Reconsider re-enabling `jobindex-rss-softwareudvikler`** (id 14,
  currently user-disabled) — still the single widest DK net we have.
- **Recruit IT html scrape — Playwright `:scope` smoke test.** The disabled
  `recruit-it` portal stub uses `:scope` as `link_selector` to target the
  wrapping `<a>`. Verify Playwright resolves `:scope` inside
  `IElementHandle.QuerySelectorAsync` before flipping `enabled: true`;
  otherwise extend `HtmlAdapter` to read attributes from the matched list
  element directly.
- **Recruit IT location parsing.** Location renders as a plain text node
  next to an icon with no wrapper class; `location_selector` is intentionally
  omitted and listings will have null location until the markup changes.
- **`jobindex-rss` / `it-jobbank-rss` dedupe warning.** Both feeds hit the
  same Jobindex backend; enabling both wastes calls. Add a config-time hint
  or a UX warning when both are enabled at once.
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
