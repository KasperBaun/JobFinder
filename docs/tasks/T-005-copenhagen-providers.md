# T-005 — Copenhagen-relevant provider seed

## Why

Today's seeded providers (`thehub`, `remotive`) work but don't represent
the user's actual market: software roles in Copenhagen, Denmark.
`jobnet`/`jobindex` are not viable as APIs (cert-auth or ToS-blocked).
The richest *public, stable* sources for Copenhagen tech roles are the
ATS feeds that local employers expose directly (Greenhouse, Lever,
Workable, Ashby) plus one general-purpose national aggregator (Adzuna).

## Outcome

A seeded `portals.example.yml` covering ~15–25 Copenhagen-relevant
sources, plus the smallest backend nudge needed to make per-company ATS
feeds practical (a static-field overlay so each board can stamp its
own company name on its listings).

## Scope

### Library

- Add `StaticFields: IReadOnlyDictionary<string, string>?` to
  `PortalConfig` — values applied to every produced `Listing` after the
  adapter maps it (e.g. `company: "Pleo"` on a Greenhouse board that
  doesn't carry the company name in its payload).
- `PortalConfigLoader` parses the optional `static_fields:` block.
- `ApiAdapter.TryBuildListing` (and ideally a shared helper on
  `BaseAdapter`) overlays static fields onto title/company/location.
  Static value wins if non-empty; otherwise mapped value.
- `RssAdapter` and `HtmlAdapter` should pick up the same overlay so the
  pattern is uniform.

### Config

- Rewrite `src/config/portals.example.yml` to ship with:
  - **Greenhouse boards** for ~10 Copenhagen-area tech employers.
    Endpoint shape: `https://boards-api.greenhouse.io/v1/boards/<slug>/jobs?content=true`.
    Mapping: `items_path: jobs`, `id: id`, `title: title`,
    `location: location.name`, `description: content`,
    `url: absolute_url`, `posted_at: updated_at`. Use
    `static_fields: { company: "<Name>" }`.
  - **Lever boards** where used. Endpoint: `https://api.lever.co/v0/postings/<slug>?mode=json`.
    Mapping: items at root (no `items_path`), `id: id`, `title: text`,
    `location: categories.location`, `description: descriptionPlain`,
    `url: hostedUrl`, `posted_at: createdAt`.
  - **Adzuna DK** as one general-purpose source. Endpoint:
    `https://api.adzuna.com/v1/api/jobs/dk/search/1?app_id=…&app_key=…&what=software&results_per_page=50`.
    Ships disabled with placeholder keys + a `notes:` block telling the
    user to register at https://developer.adzuna.com (free tier).
  - Keep `thehub` and `remotive` as-is — they're useful complements.
  - Keep `jobnet` disabled (existing comment), `jobindex`/`linkedin` as
    `manual`.

### Research the next agent owns

The candidate company list below is a starting point — the next agent
must verify each board exists and is open before adding it. Quick check:
visit the URL in a browser; if it returns JSON with `jobs:`, it's good.

Likely Greenhouse: Pleo, Vivino, Templafy, Dixa, Forecast, TwentyThree,
Falcon.io, Siteimprove, Unity Copenhagen, Zendesk Copenhagen, Tradeshift.
Likely Lever: Trustpilot, Just Eat Takeaway.
Likely Workable: Worksome, Lunar.
Drop any whose feeds 404 or require auth; replace with discoveries.

### Tests

- `PortalConfigLoaderTests` — round-trip a portal with `static_fields`.
- `ApiAdapterTests` — static field overrides empty mapped value;
  static field overrides non-empty mapped value (document the
  precedence chosen).

## Out of scope

- Editing `static_fields` from the GUI provider editor — for v1 it
  lives in YAML only. The GUI's existing round-trip preserves unknown
  blocks, so the field survives.
- Pagination — listed in *Pending engine improvements* in `todo.md`,
  worth doing alongside if the agent finds Adzuna's first page is
  consistently capped.
- Per-company secrets — the seed must not embed real Adzuna keys.
  Placeholder + instructions only.

## Requirements touched

R-020 (provider list), R-021 (provider types), R-024 (example config).
Add a new R if `static_fields` warrants one — e.g. *"The system should
let a portal config inject static field values into every produced
listing."*
