# Jobzonen.dk

**Prior:** Aggregates from Danish newspapers and partners. Backed by `jobsearch.dk`.

**Check:** RSS / partner feed? Try `/rss`, `/feed`, network tab. Likely shares backend with `jobsearch.dk` — confirm and decide whether the two rows collapse.

---

**Findings:**

- Homepage `https://www.jobzonen.dk/` was unreachable from the research environment (ECONNREFUSED on both `www` and apex), so live network-tab inspection was not possible. Public sources on the operator relationship were used instead.
- Wikipedia (da) on Jobzonen: "I 2019 blev Jobzonen.dk relanceret af Jobsearch.dk." Jobzonen also routes employer enquiries to `vip@jobsearch.dk`. The site is a Jobsearch.dk-operated frontend on the same listing pool, not an independent aggregator with its own data layer.
- No separate Jobzonen API or RSS feed is advertised in any public source examined. The only documented public feed in this family is `https://www.jobsearch.dk/feed/job-annoncer` (covered in the [`jobsearch.md`](jobsearch.md) worksheet).
- Conclusion: enabling Jobzonen alongside Jobsearch.dk would produce duplicate listings against the same backend. Pick Jobsearch.dk as canonical; treat Jobzonen as a duplicate row.

**Verdict:** `dead — duplicates jobsearch.dk`

**Stub block:** none — covered by the `jobsearch-dk` block in the [`jobsearch.md`](jobsearch.md) worksheet. Do not add a Jobzonen-specific block.
