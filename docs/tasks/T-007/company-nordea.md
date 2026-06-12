# Nordea (favorite-company career site)

**Prior:** User-declared preferred employer (R-091). Nordic bank, big Copenhagen engineering org.

**Findings:**

- Careers hub `nordea.com/en/careers` links out to `careers.nordea.com` — SAP SuccessFactors Career Site Builder.
- SuccessFactors has no public anonymous JSON API, **but** `https://careers.nordea.com/search-jobs` is fully server-rendered: 100 openings per page in classic CSB table markup (`tr.data-row` → `a.jobTitle-link` + `.jobLocation`), global list with Copenhagen roles present ("Senior Software Engineer", København K, 2026-06-12).
- robots.txt disallows only apply/talent-community/service paths — `/search-jobs` and `/job/...` are permitted.
- Job detail pages are also server-rendered (69 KB, full description text) → enrichBody works.

**Verdict:** `html` — wired as catalog id 41 `html-nordea` (HtmlAdapter + enrichBody; enrichment support added to HtmlAdapter for this batch).
