# SimCorp (favorite-company career site)

**Prior:** User-declared preferred employer (R-091). Copenhagen-HQ fintech (investment management software); heavy .NET shop.

**Findings:**

- ATS is Workday, tenant `simcorp`, host `wd3`, site `SimCorp_Jobs`.
- Public CXS JSON endpoint, no auth: `POST https://simcorp.wd3.myworkdayjobs.com/wday/cxs/simcorp/SimCorp_Jobs/jobs` → 200, ~273 global roles unfiltered; `searchText: "developer"` narrows to ~60 engineering roles (2026-06-12).
- Items include `locationsText`. Same Workday caveats as LEGO: relative `postedOn` (null date), JS job pages with JSON-LD content (enrichBody works).

**Verdict:** `api` — wired as catalog id 38 `workday-simcorp`.
