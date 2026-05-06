# Workindenmark.dk

**Prior:** Public sector portal under Beskæftigelsesministeriet. Likely sources from Jobnet + EURES — may just be a frontend over those, not its own data source.

**Check:** Direct API/RSS, or a frontend over Jobnet/EURES? If the latter, mark `dead` (integrating both ends doubles up on STAR/EURES). If it's its own thing, capture endpoint.

---

**Findings:**

- `https://www.workindenmark.dk` is an informational/landing site under STAR. The "find a job" CTA forwards to `https://workindenmark.jobnet.dk/` — i.e. a Jobnet-hosted subdomain. The actual search runs on the Jobnet backend, not a separate Workindenmark backend.
- `https://www.workindenmark.dk/about-workindenmark-eures` confirms the role: "Workindenmark is part of the European Recruitment Services (EURES)" and "leverages the EURES network." It is the Danish EURES front-office, not an independent platform.
- No `/api`, `/rss`, `/feed`, `/jobs.rss` surfaced. No developer/partner link. No `<link rel="alternate" type="application/rss+xml">` on the homepage.
- Job vacancies displayed on Workindenmark are the English-language subset of Jobnet (~2–3k EN-language postings) plus EURES cross-border feeds. Same upstream as jobnet (STAR JobannonceService) — gated behind `spoc@star.dk` (see jobnet.md).
- Third-party EURES scrapers exist on Apify (`lexis-solutions/eures-eu-jobs-scraper`, `easyapi/eures-job-scraper`), and Norway publishes `navikt/pam-eures-stilling-eksport` on GitHub for the EURES *export* side, but DK doesn't expose its EURES exporter publicly. Nothing usable as a free public endpoint.
- Conclusion: integrating Workindenmark would be a strict subset of integrating Jobnet, and the upstream is the same gated STAR webservice. No independent value.

URLs checked:
- https://www.workindenmark.dk/
- https://www.workindenmark.dk/about-workindenmark-eures
- https://workindenmark.jobnet.dk/

**Verdict:** `dead` — frontend over Jobnet (subdomain `workindenmark.jobnet.dk`) plus EURES cross-border feeds; same STAR upstream as Jobnet, no independent API/RSS. Integrating it would double up on the gated jobnet source.

**Stub block:** _none_ (verdict is `dead`; per T-007 method, no block is added).
