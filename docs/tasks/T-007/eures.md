# EURES (eures.europa.eu)

**Prior:** EU job portal. Multiple third-party scrapers (Apify) exist, suggesting no clean public REST API for *consumers*. National-level EURES portals (e.g., Norway's `pam-eures-stilling-eksport` on GitHub) implement *input* APIs to push to EU, not pull. The frontend at `eures.europa.eu/eures-portal/jv-se/home` does an XHR JSON call to render results — that endpoint is the most likely candidate.

**Check:** Open a DK-filtered search on `eures.europa.eu`, watch the network tab, capture the JSON XHR endpoint and request shape. Probe with a curl to confirm it works unauthenticated. If yes, document the mapping and add a stub. If the endpoint requires session cookies / CSRF tokens, mark `dead` (we don't bypass anti-bot per CLAUDE.md).

---

**Findings:**

- The portal is an Angular SPA (`/eures/portal/main.<hash>.js`). The bundle exposes a fixed gateway map of internal microservices:
  - `jvseBaseUrl: /eures/api/jv-searchengine` (job vacancies — what we want)
  - `cvseBaseUrl: /eures/api/cv-searchengine`
  - `sharedDataBaseUrl: /eures/api/shared-data-rest-api`
  - `umBaseUrl: /eures/api/um`, `orgBaseUrl: /eures/api/organisation`,
    `nmBaseUrl: /eures/api/newsletter-management-rest-api`,
    `emBaseUrl: /eures/api/employer-management-api`
- The actual search XHR is `POST https://europa.eu/eures/api/jv-searchengine/page/search` (with a sibling `/page/search/html/view` for rendered fragments). Anonymous probes — both unauth GET and POST with browser-like `Origin` / `Referer` / JSON body — return:
  - `GET …/page/search` → `401 Unauthorized` (response header: `Proxy-support: Session-based-authentication`).
  - `POST …/page/search` → `403 An expected CSRF token cannot be found`.
- The Angular HTTP interceptor reads the `XSRF-TOKEN` cookie and echoes it as the `X-XSRF-TOKEN` header (constants `CSRF_COOKIE_NAME=XSRF-TOKEN`, `CSRF_HEADER_NAME=x-xsrf-token` are present in the bundle). No `Set-Cookie: XSRF-TOKEN=…` is issued by any anonymous request I tried, including:
  - `GET /eures/api/jv-searchengine/public/properties` (200 JSON, no cookie)
  - `GET /eures/api/um/public/security/profile` (200, returns an anonymous `userId:null` profile, no cookie)
  - `GET /eures/portal/jv-se/home` (200 HTML, no cookie)
  - `POST /eures/api/jv-searchengine/page/search` (403, no cookie)
  - Tried sibling paths `/public/page/search`, `/public/search`, `/public/page/search/html/view`, `/csrf`, `/public/csrf` — all 404.
- The `XSRF-TOKEN` cookie is only minted as part of an authenticated session (the `Proxy-support: Session-based-authentication` header indicates EU Login). The only consumer-friendly path forward is to scrape the rendered SPA, which is what the existing Apify actors (`lexis-solutions/eures-eu-jobs-scraper`, `easyapi/eures-job-scraper`) do. CLAUDE.md is explicit: "No anti-bot bypassing. Sites that block automation are supported only via the `manual` provider type."
- One unauth endpoint that *does* return JSON is `GET /eures/api/jv-searchengine/public/properties`, but it only exposes display flags (e.g. `jvse.max.faj.search.results: 10000`) — no vacancy data.
- No "Developers" / "Partners" / "API" / "RSS" link is exposed on `eures.europa.eu`. The closest open data offering is the EURES *input* API (used by national PES like NAV-Norway to *push* vacancies *to* EU), which is the wrong direction for us.

**Verdict:** `manual` — there is a clear backend search endpoint (`POST /eures/api/jv-searchengine/page/search`), but it's CSRF-gated behind an authenticated EU-Login session and there is no public/anonymous JSON variant. Bypassing the session/CSRF flow is out of scope per CLAUDE.md. EURES jobs are largely re-listings of national PES vacancies anyway, so the unique-coverage cost of skipping it is low. User can still hit the SPA in the browser.

**Stub block:**

```yaml
- name: eures
  type: manual
  enabled: false
  homepage: https://europa.eu/eures/portal/jv-se/home?lang=en
  search_url_template: https://europa.eu/eures/portal/jv-se/search?lang=en&page=1&resultsPerPage=10&orderBy=MOST_RECENT&locationCodes=dk
  notes: |
    EU-wide job portal aggregating national PES vacancies. The SPA's
    backend search endpoint is `POST /eures/api/jv-searchengine/page/search`
    (returns JSON), but it is CSRF-gated and the `XSRF-TOKEN` cookie is
    only minted inside an EU-Login session — `Proxy-support:
    Session-based-authentication` header on 401 responses, no `Set-Cookie`
    on any anonymous probe. Bypassing this is anti-bot circumvention and
    out of scope per CLAUDE.md. Use the search_url_template to open a
    pre-filtered DK search in the browser and review manually.
    Existing third-party scrapers on Apify confirm there is no clean
    consumer API; they all rely on browser automation.
    Re-evaluate if the European Commission ever publishes an open
    consumer-side JV API (the national-PES-input API at
    `pam-eures-stilling-eksport` is the wrong direction — push, not pull).
```
