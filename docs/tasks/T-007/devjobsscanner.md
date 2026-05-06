# DevJobsScanner.com

**Prior:** Tech aggregator across 15+ platforms. Aggregates *other* sources' listings — most or all of its volume duplicates the per-company ATS feeds we already have via T-005. Redistribution rights unclear.

**Check:** Public API / RSS? If yes, does it explicitly permit external consumption? If no clear feed *and* no permission, mark `dead`. Even if reachable, value is low — we'd rather hit the underlying ATS feeds directly to avoid the aggregation layer.

---

**Findings:** WebFetch of `https://www.devjobsscanner.com/` and `https://devjobsscanner.com/about` surfaced no API documentation, RSS link, `/feed`, `/api`, or developer/partner section. The site is a consumer-facing search UI; no public terms permitting programmatic redistribution. Its value proposition is *re-aggregating* listings from Greenhouse / Lever / Workable / LinkedIn / etc. — the same upstream ATS endpoints we already poll directly via T-005, so any data we'd pull would be a strictly redundant (and lossier) copy versus the source feeds.

**Verdict:** `dead` — no public feed, unclear redistribution rights, and zero net new coverage over the ATS feeds already wired. Not worth a manual stub either.

**Stub block:** _n/a_
