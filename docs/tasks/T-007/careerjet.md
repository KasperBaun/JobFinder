# Careerjet.dk

**Prior:** Public Search API located: `https://search.api.careerjet.net/v4/query` (HTTP GET). Affiliate ID is mandatory; free to register at `careerjet.com/partners/api`. Open-source clients (Python, PHP, Ruby) exist on GitHub under the `careerjet/` org.

**Check:** Confirm the response shape (`jobs[]` with `title`, `company`, `locations`, `description`, `url`, `date`?). Capture required GET params (`keywords`, `location`, `affid`, `locale_code=da_DK`?). The `careerjet-api-client-python` README has a working example to crib from.

---

**Findings:**

Endpoint: `https://search.api.careerjet.net/v4/query` (HTTPS GET, JSON).

Required GET params (per `careerjet.com/partners/api` and the official Python client at `github.com/careerjet/careerjet-api-client-python`):

- `affid` — affiliate ID (free; register at `careerjet.com/partners/api`)
- `user_ip` — IP of the end user triggering the call
- `user_agent` — UA string of the end user
- `url` — URL of the results page on the caller's site
- `locale_code` — language/region (e.g. `da_DK`); defaults to `en_GB` if omitted, but the Python client treats it as required and passes it on every call

Optional GET params used for searching: `keywords`, `location`, `sort` (`relevance|date|salary`), `page`, `page_size` (1–100, default 20), `offset` (0–999), `radius` (km), `contract_type` (`p|c|t|i|v`), `work_hours` (`f|p`), `fragment_size` (excerpt length, default 120).

Response top-level keys: `type`, `hits`, `message`, `pages`, `response_time`, `jobs` — items array is at `jobs`.

Per-item fields: `title`, `company`, `date` (string), `description`, `locations` (string), `salary`, `salary_currency_code`, `salary_max`, `salary_min`, `salary_type` (`Y|M|W|D|H`), `site`, `url`. There is no documented per-item `id`; the `url` is the natural unique key (the dedupe layer falls back to URL when `id` is empty).

Live probe of `https://search.api.careerjet.net/v4/query?keywords=software&location=Copenhagen&affid=YOUR_AFFID&user_ip=8.8.8.8&user_agent=Mozilla&url=https://example.com&locale_code=da_DK` returned HTTP 401 — confirms `affid` is enforced server-side and a registered key is required even to inspect the payload. Field list above is taken from the partner-API docs and the Python client.

Citations:
- `https://www.careerjet.com/partners/api` — params + response key list (`jobs`, `hits`, etc. and per-item field names).
- `https://github.com/careerjet/careerjet-api-client-python/blob/master/README.md` — confirms `affid`, `user_ip`, `user_agent`, `url`, `locale_code` are mandatory in practice.

**Verdict:** `api` — fits the existing `ApiAdapter` (GET + JSON + simple `items_path`/field mapping). Only friction is the free `affid` registration; ship disabled with a `notes:` block telling the user to register.

**Stub block:** Ready to paste into `src/config/portals.example.yml` under the "Disabled by default" section, alongside `adzuna-dk`:

```yaml
  - name: careerjet-dk
    type: api
    enabled: false
    endpoint: https://search.api.careerjet.net/v4/query
    query_params:
      affid: "YOUR_AFFID"
      user_ip: "8.8.8.8"
      user_agent: "Mozilla/5.0"
      url: "https://example.com/jobs"
      locale_code: "da_DK"
      keywords: "software"
      location: "Copenhagen"
      page_size: 100
      sort: "date"
    response_mapping:
      items_path: "jobs"
      id: "url"
      title: "title"
      company: "company"
      location: "locations"
      description: "description"
      url: "url"
      posted_at: "date"
    rate_limit_rps: 1.0
    notes: |
      Careerjet DK — Danish coverage of the global Careerjet aggregator
      (free tier). Register at https://www.careerjet.com/partners/api to
      get an affid, paste it into query_params above, then flip
      enabled: true. The user_ip / user_agent / url params are required
      by Careerjet on every request — the placeholder values are fine
      for personal use; substitute your own if you prefer. There is no
      per-job id in the response, so url is used as the dedupe key.
```
