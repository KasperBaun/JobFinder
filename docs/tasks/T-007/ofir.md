# Ofir.dk

**Prior:** Owned by Jobindex (acquired from North Media, Jan 2025). Historic RSS feeds may have existed pre-acquisition.

**Check:** RSS feed at `ofir.dk/rss`, `/feed`, or under category pages? Or has the backend merged into Jobindex post-acquisition (in which case → same verdict as Jobindex)? If RSS is reachable, capture the URL pattern and a sample item shape.

---

**Findings:**

The Ofir brand has been **fully retired into Jobindex** post-acquisition. No code path on `ofir.dk` returns Ofir-branded content.

- `https://www.ofir.dk` → `301 Moved Permanently` to `https://www.jobindex.dk/?ref=ofir`. The whole hostname redirects, not just the homepage.
- `https://www.ofir.dk/rss` → `302 Found` to `https://www.jobindex.dk/?ref=ofir_unhandled`. The `ref=ofir_unhandled` query string is the Jobindex backend explicitly logging "someone hit an Ofir URL we don't have a handler for, dump them on the homepage."
- No subdomain or alternative host (`api.ofir.dk`, `feeds.ofir.dk`) was discoverable; web search returned no Ofir-specific RSS or API references.
- Pre-acquisition Ofir feeds (if any existed under North Media) are gone. There is nothing Ofir-specific to scrape — every URL ends up on Jobindex.

Operational implication: enabling the `jobindex-rss` block in the Jobindex worksheet already gets you whatever Ofir would have surfaced. There is no separate Ofir adapter to build.

**Verdict:** `dead` — domain redirects whole-host to `jobindex.dk`; no Ofir-branded backend remains. Use the `jobindex-rss` stub instead.

**Stub block:** _none — covered by `jobindex-rss`._
