# todo

Working list of unfinished work. Landed changes are in git history.

## Pending engine improvements

- **Country → region mapping for cross-EU remotes.** A listing in "Germany"
  for an EU-region user falls to else tier (0.1) because the matcher only
  recognises "EU/Europe/EMEA" synonyms, not member states. Add a small
  hardcoded EU-country list (DE, FR, NL, SE, NO, FI, IS, IE, ES, IT, AT,
  BE, LU, PL, CZ, etc.) so Region: "EU" matches any of them. Or expose
  `region_countries:` in the skillset for explicit control.


- **ApiAdapter pagination.** The implementation plan mentions "supports
  offset/page pagination if configured" but `ApiAdapter.FetchAsync` does a
  single GET. Add a `pagination:` block to `PortalConfig` (`offset_param`,
  `page_size`, `max_pages`) and loop with a safety cap.
- **Rate limiting.** `PortalConfig.RateLimitRps` is parsed but not honored
  anywhere. Worth wiring once pagination lands (single-request adapters
  don't benefit). Simple semaphore + delay per portal.
- **CSV quoted-newline support.** `ManualAdapter.ReadCsvFile` uses
  `StreamReader.ReadLine()`, which splits inside quoted fields that span
  lines. Upgrade to a stream-based parser.
- **CancellationToken plumbing.** `ListingsCommand.ExecuteAsync` doesn't
  pass a CT to adapters — Ctrl+C only bails after the current adapter finishes.
- **Playwright install path hardcoded.** `HtmlAdapter` and `ConfigVerifier`
  reference `bin/Debug/net10.0/playwright.ps1`; Release builds use a
  different path. Resolve via `AppContext.BaseDirectory` or similar.
- **`ApiAdapter.RenderTemplate` silently drops unknown template keys.**
  Consider warning or throwing when a template key isn't in the item.
- **Remove unused `PortalConfig.BaseUrl`.** Parsed and stored, never read.
- **Additional `--explain` test.** The two previously-skipped tests now
  pass; add one more: `--explain` on an INCLUDED listing prints the full
  breakdown with state = `INCLUDED`.

## Nice-to-haves / polish

- Live smoke against `jobnet.dk` behind an env flag (off in CI).
- YAML frontmatter at the top of `top_jobs.md` for downstream tooling.
- Markdown table escaping for `|` in titles/companies (currently only
  `VerificationReport.ToMarkdown` escapes; `MarkdownReportWriter` does
  not — not a bug for tech job titles, but cheap to add).
- Split `CliApp.Create(console?)` out of `CliApp` into a separate test
  seam so production `Program.cs` keeps the parameterless overload.

## Bugs spotted but deliberately deferred

- `ScoreSeniority` adjacent case returns `(0.5, true)` — reasoning says
  "Seniority fits" for half-credit adjacent matches. Cleanest fix is a
  proper tri-state (full/partial/none/unknown) on `MatchReasoning`;
  requires model extension.
- `InferSeniority` looks only at the title, not the description. Listings
  that spell out "senior" only in the body get classified as `null`.
