# LinkedIn

**Prior:** `manual` stub already in `portals.example.yml`. LinkedIn has no public job-search API for individuals; the Jobs API requires a Talent Solutions partnership and is gated.

**Check:** Verify still no public tier in 2026. If by some miracle one exists, capture endpoint + auth + rate-limit terms. Otherwise, leave the verdict at `manual` with a one-line confirmation.

---

**Findings:** Confirmed via web search (May 2026). Microsoft Learn's LinkedIn Talent docs (`learn.microsoft.com/.../job-postings/api/overview?view=li-lts-2026-03`) restrict access to approved partners only. Per multiple 2026 guides (e.g. `connectsafely.ai/articles/linkedin-api-complete-guide-2026`, `getphyllo.com/post/linkedin-api-ultimate-guide-on-linkedin-api-integration`), all jobs APIs require Talent Solutions Partner Program approval since 2015 and LinkedIn is "currently not accepting new partnerships" for the Job Posting API. The free tier exposes only Sign-in with LinkedIn (name, photo, headline) — no jobs data. Third-party scrapers exist (Apify, Coresignal, Unipile) but rely on bypassing or licensed redistribution and violate LinkedIn ToS for unaffiliated users.

**Verdict:** `manual` — no public jobs API in 2026; Talent Solutions is partner-gated and closed to new applicants.

**Stub block:** _n/a_ (existing `linkedin` manual stub in `portals.example.yml` is correct as-is)
