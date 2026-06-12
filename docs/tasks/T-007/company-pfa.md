# PFA (favorite-company career site)

**Prior:** User-declared preferred employer (R-091). Pension/insurance, Copenhagen HQ.

**Findings:**

- ATS is SuccessFactors (`career2.successfactors.eu`, company `pfapensionP`) — no public anonymous JSON.
- PFA's own page `https://www.pfa.dk/om-pfa/job-i-pfa/ledige-job/` **server-renders** the list (~5 openings, 2026-06-12, incl. "AI Arkitekt"): `li.jobs-listing__list-item` (minus the `--header` variant) → title in a `<button>`, link in `a.cta-btn` (absolute SuccessFactors URL with `career_job_req_id`).
- pfa.dk robots.txt disallows only `/soegning/` and PDFs — the job page is permitted.
- No per-item location in the markup; all PFA roles are Copenhagen HQ → `staticFields.location = "København, Danmark"`.
- The legacy SuccessFactors job pages are server-rendered → enrichBody works.

**Verdict:** `html` — wired as catalog id 43 `html-pfa`.
