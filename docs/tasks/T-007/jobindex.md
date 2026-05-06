# Jobindex.dk

**Prior:** Already classified `manual` in `src/config/portals.example.yml` — no public API, ToS-blocked. Owns Ofir, Stepstone, IT-jobbank as of Jan 2025, so its backend may now back several other portals on this list.

**Check:** Any partner / affiliate / RSS feed exposed publicly in 2026? Likely still no — but a 5-minute confirmation is worth recording so the next agent doesn't redo it.

---

**Findings:**

The prior `manual` verdict turns out to be too conservative — Jobindex *does* expose a public RSS feed, just not advertised on the homepage.

- Homepage (`https://www.jobindex.dk`) shows no Developer / API / RSS link in the chrome (only employer-facing pages: `/virksomheder/jobannoncering`, `/virksomheder/rekruttering`).
- `robots.txt` (`https://www.jobindex.dk/robots.txt`) blocks `/api/` and many `/jobsoegning/` filtered variants but **does not** block `/jobsoegning.rss`. Sitemap: `https://www.jobindex.dk/sitemap.gz`.
- `https://www.jobindex.dk/rss` → 404.
- `https://www.jobindex.dk/jobsoegning.rss` → **valid RSS 2.0**. Returns latest postings with `title`, `link` (e.g. `https://www.jobindex.dk/vis-job/h1662717`), `pubDate` (RFC 822 with `+0200`), and `description`.
- `https://www.jobindex.dk/jobsoegning.rss?q=software+engineer` → **valid filtered RSS**. Channel title becomes `"Jobindex - Ledige job indeholdende mindst et af ordene software eller engineer"`, confirming the `q=` parameter is honoured (jobindex's `q=` is OR-of-words, not strict phrase). Other filter params from the HTML search (e.g. `area=`, `subid=`, `geoareaid=`) presumably carry over but were not exhaustively tested.
- The same Jobindex backend now powers `it-jobbank.dk/jobsoegning.rss` (confirmed working), and `ofir.dk` 301-redirects whole-domain to `jobindex.dk` — so jobindex's RSS is effectively the family feed for three of the four portals in this batch. **Stepstone.dk does not share this RSS pattern** (`/jobsoegning.rss` 404s on that host).

ToS / fair-use caveat: the feed is unauthenticated and undocumented. Treat it as best-effort; don't hammer it. RSS items only carry the description snippet, not the full posting body — full text needs a follow-up fetch of `link`, which is exactly the existing `RssAdapter` shape.

**Verdict:** `rss` — `https://www.jobindex.dk/jobsoegning.rss` is a working RSS 2.0 feed and accepts a `q=` keyword filter. Replaces the prior `manual` classification.

**Stub block:**

```yaml
  - name: jobindex-rss
    type: rss
    enabled: false
    endpoint: https://www.jobindex.dk/jobsoegning.rss
    query_params:
      q: "software engineer"
    rate_limit_rps: 0.5
    notes: |
      Jobindex.dk public RSS feed (undocumented but stable as of 2026-05).
      Returns latest postings; `q=` accepts space-separated keywords matched
      as OR-of-words, not strict phrase. Channel title echoes the filter so
      you can sanity-check what got applied. Items expose title, link
      (e.g. https://www.jobindex.dk/vis-job/h1662717), pubDate, and a short
      description; full body requires fetching the link.
      The same backend powers it-jobbank.dk/jobsoegning.rss and ofir.dk
      (which now 301-redirects to jobindex.dk). Run only one of jobindex /
      it-jobbank as enabled to avoid heavy duplicates — the dedupe layer
      will drop the rest, but you waste calls. Keep rate_limit_rps low;
      this feed is unadvertised and ToS does not bless automation.
```
