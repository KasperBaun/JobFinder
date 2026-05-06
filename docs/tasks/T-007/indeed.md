# Indeed.dk

**Prior:** Indeed Publisher API was deprecated several years ago. Indeed announced its **Single-Source Feed Policy (Effective March 31, 2026)** which removed visibility for free single-source XML feeds — past today's date (2026-05-06). Multi-source / partner feeds still exist but require a paid partnership.

**Check:** Verify the post-cutoff state. For our local-tool use case (no partnership, free consumption), the path is `dead`. Confirm and write a one-line note.

---

**Findings:** Confirmed via Indeed's [Single-Source Feed Policy announcement](https://indeedinc.my.site.com/employerSupport1/s/article/Single-Source-Feed-Policy-Effective-March-31-2026?language=en_US) and [HR Dive coverage](https://www.hrdive.com/news/visibility-ends-for-certain-free-single-source-xml-feeds-on-indeed/816209/). Free single-source XML feeds dropped visibility on the 2026-03-31 cutoff.

**Verdict:** dead — no free / public path remains for an external tool to consume Indeed.dk listings as of May 2026. Indeed Apply XML is a posting-side workflow for employers; not relevant to an external aggregator.

**Stub block:** _n/a_
