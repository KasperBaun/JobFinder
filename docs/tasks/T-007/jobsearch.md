# Jobsearch.dk

**Prior:** Backs Jobzonen.dk (Danish job search engine). Likely the underlying feed for Jobzonen rather than a separate frontend.

**Check:** Same feed as Jobzonen, or a separate API/RSS? If same, this row collapses into Jobzonen. If separate, capture the endpoint shape.

---

**Findings:**

- Homepage `https://www.jobsearch.dk/` advertises itself as "Danmarks største jobportal".
- Footer exposes a public RSS 2.0 feed: `https://www.jobsearch.dk/feed/job-annoncer` (linked as "Nye jobs (RSS)"). The feed is open — no auth, no key.
- Confirmed live fetch: feed contains ~100 items. Each `<item>` has `title`, `description`, `link`. No `pubDate` is published. Listing URLs follow the pattern `https://www.jobsearch.dk/<role-slug>/<city-slug>/<numeric-id>`.
- No public REST/JSON API surfaced from the homepage. No "developer", "API", or "partners" link in the footer. Integration enquiries are routed to `support@jobsearch.dk` / `vip@jobsearch.dk`.
- **Backend relationship with Jobzonen confirmed.** Wikipedia (da): "I 2019 blev Jobzonen.dk relanceret af Jobsearch.dk." Jobzonen also lists `vip@jobsearch.dk` as the contact for getting employer listings onto the site. Jobsearch.dk is the canonical source; Jobzonen is a downstream frontend on the same listing pool.

**Verdict:** `rss` — canonical source for the Jobzonen/Jobsearch listing pool.

**Stub block:**

```yaml
  - name: jobsearch-dk
    type: rss
    enabled: false
    endpoint: https://www.jobsearch.dk/feed/job-annoncer
    rate_limit_rps: 1.0
    notes: |
      Jobsearch.dk — open RSS 2.0 feed, ~100 newest postings, no auth.
      Items expose title / description / link only (no pubDate, no
      company/location fields — those have to be inferred from the
      title or the slug in the URL `/{role}/{city}/{id}`).
      Same backend as Jobzonen.dk (Jobzonen was relaunched on top of
      Jobsearch.dk in 2019). Enable jobsearch-dk *or* jobzonen, not
      both — they produce the same listing pool and would dedupe to
      noise. jobsearch-dk is the canonical pick because the RSS link
      points directly at the source domain.
```
