# Jooble.org

**Prior:** Public REST API: register at `https://jooble.org/api/about` to get a key, then `POST https://jooble.org/api/{api_Key}` with a JSON body. Free tier exists.

**Check:** Confirm the JSON body shape (likely `{ keywords, location, page }`), Denmark filtering (probably `location: "Denmark"` or DK city), rate limits, and response shape (likely `{ jobs[] }` with `title`, `location`, `snippet`, `salary`, `source`, `link`). Document body + response mapping.

**Note:** The current `ApiAdapter` uses GET. Jooble is POST + JSON body — confirm the adapter handles this, or flag as an adapter extension needed.

---

**Findings:**

- **Endpoint:** `POST https://jooble.org/api/{api_Key}` — key goes in the URL path, not a header. Free key issued via the form at `https://jooble.org/api/about` (name, email, website, etc.).
- **Request body (JSON):**
  - `keywords` (string, required) — search terms.
  - `location` (string, required) — geographic area. No documented Denmark-specific syntax; conventional usage is a free-text country or city string (e.g. `"Denmark"`, `"Copenhagen"`). Confirm against live results once a key is registered.
  - `radius` (int, optional) — km; allowed values `0, 4, 8, 16, 26, 40, 80`.
  - `salary` (int, optional) — minimum wage threshold.
  - `page` (int, optional) — result page number.
  - `ResultOnPage` (int, optional) — page size cap.
  - `SearchMode` (int, optional) — defaults to `0`.
  - `companysearch` (bool, optional) — `true` searches by company name; `false` searches titles/descriptions.
- **Response (JSON):**
  - Root: `{ totalCount: int, jobs: [...] }`.
  - Per-item fields: `title`, `location`, `snippet`, `salary`, `source`, `type`, `link`, `company`, `updated` (timestamp), `id`.
- **Errors:** `403` (invalid API key), `404` (endpoint unavailable). No published rate limits or per-day cap on the free tier — assume polite throttling (~1 rps) until measured.
- **Adapter compatibility (BLOCKER):** `src/Jobmatch/Adapters/ApiAdapter.cs` is hardcoded to `HttpMethod.Get` and uses `BuildRequestUri` to fold `query_params` into the URL. There is no code path for `method: post`, no `body_template:` mapping, and no JSON serialisation of a request body. Jooble cannot be wired up against the current adapter.

**Verdict:** `api` — *contingent on a small ApiAdapter extension*. The data shape is clean and maps 1:1 to the existing `response_mapping` block (root key `jobs`, fields `id`/`title`/`company`/`location`/`snippet`/`link`/`updated`). The only missing piece is POST + JSON body support. If the extension is rejected, fall back to `manual` (the public site is searchable but offers no RSS).

**Adapter extension needed (follow-up, ~half-day scope):**

1. Add optional `Method` (default `GET`) and `BodyTemplate` (`IReadOnlyDictionary<string, object?>`) to `PortalConfig`.
2. In `ApiAdapter.FetchAsync`: when `Method == POST`, build `HttpMethod.Post`, serialise `BodyTemplate` via `JsonContent.Create(...)`, and skip `BuildRequestUri`'s query-param folding (or keep it — both compose).
3. Path templating: support `{api_key}` substitution in `endpoint` so the secret stays in `query_params`/`headers` rather than being baked into the URL string.
4. One xUnit test: POST adapter against a stubbed `HttpMessageHandler` that asserts `Content-Type: application/json` and the serialised body.

This keeps the existing GET path untouched and unblocks any other POST+JSON portal (several DK aggregators follow the same pattern).

**Stub block:**

```yaml
  - name: jooble
    type: api
    enabled: false
    method: post                                    # NOT YET SUPPORTED — see notes
    endpoint: https://jooble.org/api/{api_key}      # path templating NOT YET SUPPORTED
    headers:
      Content-Type: application/json
    body_template:                                  # NOT YET SUPPORTED — see notes
      keywords: "software engineer"
      location: "Denmark"
      radius: 0
      page: 1
      ResultOnPage: 50
      SearchMode: 0
    response_mapping:
      items_path: "jobs"
      id: "id"
      title: "title"
      company: "company"
      location: "location"
      description: "snippet"
      url: "link"
      posted_at: "updated"
    rate_limit_rps: 1.0
    notes: |
      Jooble.org — global aggregator with Danish coverage (free tier).
      Register at https://jooble.org/api/about to obtain an api_key,
      then substitute it into the endpoint path (replace {api_key}),
      and flip enabled: true.

      ADAPTER GAP: The current ApiAdapter (src/Jobmatch/Adapters/
      ApiAdapter.cs) is GET-only and does not support `method: post`,
      `body_template:`, or `{api_key}` path substitution. This stub is
      written against a planned extension; do not enable until that
      lands. Tracked as a follow-up: extend ApiAdapter to (a) accept
      `method` (default GET), (b) serialise `body_template` as JSON
      when method=POST, (c) interpolate `{api_key}`-style placeholders
      in `endpoint` against `query_params`/`headers`.

      No published rate limits on the free tier; 1.0 rps is a guess.
      Errors: 403 = bad key, 404 = wrong endpoint.
```
