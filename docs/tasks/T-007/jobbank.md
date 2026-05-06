# Jobbank.dk (Akademikernes Jobbank)

**Prior:** Primarily academic roles, but broad in practice. No known public API.

**Check:** RSS / partner feed? `/rss`, `/feed`, network tab on a search page. Akademikernes is a union umbrella organisation; check if there's a partner program.

---

**Findings:** WebFetch of `https://www.jobbank.dk/` showed no `/rss`, `/feed`, `/api`, "partner", "udvikler" or "developer" links anywhere on the page — the public surface is purely a search UI plus profile creation. Direct probes of `https://www.jobbank.dk/rss` and `https://www.jobbank.dk/feed` both returned 404. Web search ("akademikernes jobbank rss feed api") returned no documented feeds, only generic third-party RSS-generation services. Akademikernes is a union umbrella; there's no developer/partner program for outside consumption.

**Verdict:** `manual` — no public RSS or API; consumption would require HTML scraping behind the search UI (not in scope for this lightweight pass) or a manual CSV import via the existing `jobindex`-style manual pattern.

**Stub block:** _n/a_ (would mirror the existing `jobindex` manual stub if a user wants imports; not needed by default)
