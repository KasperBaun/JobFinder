# Stepstone.dk

**Prior:** Part of Jobindex group since 2014. Stepstone Germany has historically had a partner program — not necessarily exposed for the DK domain.

**Check:** Does `stepstone.dk` expose RSS / partner feed independently? Try `/rss`, `/feed`, watch the network tab on a search-result page for a JSON XHR. Confirm whether DK shares backend with Stepstone DE.

---

**Findings:**

Despite being in the Jobindex group since 2014 and now alongside it-jobbank, the `stepstone.dk` domain does **not** inherit the Jobindex `/jobsoegning.rss` pattern. Every common feed URL 404s.

- Homepage (`https://www.stepstone.dk`) — only employer-facing nav links (`/for-virksomheder`, `/cv/soeg/demo`, `/virksomhed/annonceringsloesninger`). No Developer/API/RSS link.
- `robots.txt` (`https://www.stepstone.dk/robots.txt`) — blocks `/api/` and filtered `/jobsoegning/` variants. Sitemap: `https://www.stepstone.dk/sitemap.gz`. No RSS path mentioned.
- Tried URL probes:
  - `/rss` → 404
  - `/feed` → 404 (note: `/feed` is *not* the StepStone employer login; that was a redirect to a different page; the request to `https://www.stepstone.dk/feed` itself returns 404)
  - `/jobs.rss` → 404
  - `/jobsoegning.rss` → 404
  - `/jobsoegning.rss?q=developer` → 404
  - `/job/rss` → 404
- `api.stepstone.com` (the Stepstone group "API knowledge base") is a **partner job-distribution system**, not a public listings API. It documents Pull Feed / Push Feed for ATS vendors and recruitment agencies pushing vacancies *into* StepStone's network (DE/AT/BE/UK + brands like HotelCareer/Gastrojobs). Requires a business contract; no consumer-side endpoint for searching DK listings. Not what jobfinder needs.
- `stepstone.dk` does **not** appear to share its public surface with Stepstone DE — DE has its own domain and its own (also private) API. No federated public feed was found for DK.
- LinkedIn / partner ATS connectors (Bullhorn, Greenhouse, Lever) noted in StepStone Connect literature are also push-side only.

So: same parent company as Jobindex, completely different public posture. The DK Stepstone listings are reachable only through the HTML site or via paid B2B integration.

**Verdict:** `manual` — no public RSS, no public API; `api.stepstone.com` is push-only B2B; `/jobsoegning.rss` is not wired on this host. CSV/manual import path only.

**Stub block:**

```yaml
  - name: stepstone-dk
    type: manual
    enabled: false
    notes: |
      stepstone.dk has no public RSS or consumer-facing API as of 2026-05.
      Despite being in the Jobindex group, the /jobsoegning.rss pattern
      that works on jobindex.dk and it-jobbank.dk returns 404 here.
      api.stepstone.com is a partner job-distribution system (Pull/Push
      Feed for ATS vendors pushing vacancies into Stepstone), not for
      consuming listings, and requires a business contract.
      To include Stepstone.dk listings, export results from the browser
      and drop a CSV/JSON at data/imports/stepstone-*.csv with columns:
      title, company, location, url, description, posted_at.
```
