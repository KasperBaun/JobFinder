# Pandora (favorite-company career site)

**Prior:** User-declared preferred employer (R-091). Jewellery group, Copenhagen HQ with a digital/tech org.

**Findings:**

- `careers.pandoragroup.com` — SuccessFactors behind a custom front. `/jobs` server-renders a 10-per-page list: `li.results-list__item` → `a.results-list__item-title--link` (relative href `/slug/job/<hash>`, resolved against the endpoint) + `.results-list__item-street--label` for location.
- robots.txt has no disallow rules for the jobs pages.
- List is all functions (retail/production/corporate) — most entries are non-tech; the ranker's title gate and stack matching filter them. Tech roles surface when posted (Copenhagen HQ).
- Job detail pages server-rendered → enrichBody works.

**Verdict:** `html` — wired as catalog id 42 `html-pandora`. Note: only the first 10 listings per fetch (pagination not followed) — acceptable, same newest-N model as the RSS feeds.
