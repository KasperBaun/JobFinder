# DFDS (favorite-company career site)

**Prior:** User-declared preferred employer (R-091). Shipping/logistics, Copenhagen HQ with a digital org.

**Findings:**

- `dfds.com/en/about/careers` (and `/careers-in-technology`, `/vacancies`) are JS apps — no ATS signature, no job hrefs, no inline API/GraphQL URLs in the raw HTML (probed 2026-06-12).
- `careers.dfds.com` 301s back to `dfds.com/en` — no separate career host.
- No public feed or anonymous JSON surfaced from static probes; the vacancy search renders entirely client-side.

**Verdict:** `manual`-style (not wired) — no catalog entry. DFDS roles arrive via the Jobindex feeds when posted there. Revisit with a browser network-tab session to find the underlying XHR if coverage matters later.
