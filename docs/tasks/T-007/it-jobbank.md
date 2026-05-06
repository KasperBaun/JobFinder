# IT-jobbank.dk

**Prior:** Jobindex-owned tech-only portal. Likely no separate API since the parent (Jobindex) is `manual`.

**Check:** Does the tech segment have its own RSS — possibly inherited from a pre-acquisition feed? `/rss`, `/feed`, category pages.

---

**Findings:**

The Jobindex parent backend exposes the same RSS endpoint on `it-jobbank.dk` that it does on `jobindex.dk` — but pre-filtered to the IT-jobbank inventory, which is a useful tech-only slice.

- Homepage (`https://www.it-jobbank.dk`) — only employer-facing links (`/for-virksomheder`, `/virksomhed/annonceringsloesninger`, `/cv/soeg/demo` for finding IT profiles). No Developer/API/RSS link surfaced.
- `robots.txt` (`https://www.it-jobbank.dk/robots.txt`) — blocks `/api/` and various filtered `/jobsoegning/` parameter variants. Sitemap: `https://www.it-jobbank.dk/sitemap.gz`. No RSS path explicitly mentioned, but **not blocked either**.
- `https://www.it-jobbank.dk/rss` → 404.
- `https://www.it-jobbank.dk/jobsoegning.rss` → **valid RSS 2.0**. Channel name `"Computerworld it-jobbank"`. Contains 30 items per response, with `title`, `link` (e.g. `https://www.it-jobbank.dk/vis-job/h1662606`), `pubDate`, short `description`. Same item shape as the jobindex.dk feed — same backend, just IT-scoped inventory.
- `https://www.it-jobbank.dk/jobsoegning.rss?q=developer` → **valid filtered RSS**. Channel title becomes `"Computerworld it-jobbank - Ledige job indeholdende ordet developer"`, confirming `q=` is honoured. Same caveat as jobindex: `q=` is OR-of-words, so multi-word queries are loose.

Same Jobindex backend infrastructure as the `jobindex-rss` block, but pre-scoped to IT positions only. This is the right thing to enable for a tech-focused jobfinder run — narrower and noisier-than-jobindex tradeoff is worth it for higher tech-job density.

Note: enabling **both** `jobindex-rss` and `it-jobbank-rss` will surface heavy overlaps (every IT-jobbank listing also appears in the jobindex inventory). The dedupe layer will drop them, but you'd be doubling RSS calls for no extra coverage. Pick one.

**Verdict:** `rss` — `https://www.it-jobbank.dk/jobsoegning.rss` is a working RSS 2.0 feed scoped to IT roles, accepts `q=` filter. Same backend as `jobindex.dk`.

**Stub block:**

```yaml
  - name: it-jobbank-rss
    type: rss
    enabled: false
    endpoint: https://www.it-jobbank.dk/jobsoegning.rss
    query_params:
      q: "developer"
    rate_limit_rps: 0.5
    notes: |
      IT-jobbank.dk public RSS feed (undocumented but stable as of 2026-05).
      Same Jobindex backend as the `jobindex-rss` block, but pre-scoped to
      IT-only inventory — narrower and tech-denser. `q=` accepts
      space-separated keywords matched as OR-of-words; channel title
      echoes the applied filter for sanity-check. Items expose title,
      link (e.g. https://www.it-jobbank.dk/vis-job/h1662606), pubDate,
      and a short description; full body requires fetching the link.
      Heavy overlap with `jobindex-rss` — enable one or the other, not
      both, unless you want the dedupe layer to drop ~90% of the smaller
      feed. Keep rate_limit_rps low; this feed is unadvertised and ToS
      does not bless automation.
```
