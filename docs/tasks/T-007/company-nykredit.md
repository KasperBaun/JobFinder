# Nykredit (favorite-company career site)

**Prior:** User-declared preferred employer (R-091). DK mortgage bank, Copenhagen.

**Findings:**

- `nykredit.dk` frontpage and `nykredit.com/karriere/` + `/karriere/ledige-stillinger/` carry no ATS signature, no job-host links, and no inline API URLs in raw HTML (probed 2026-06-12) — the vacancy list renders client-side.
- `nykredit.dk/karriere` and `/om-os/job-og-karriere/` 404.
- Good news: Nykredit posts on Jobindex — the `+backend +udvikler` RSS feed (catalog id 30) carried "Senior React Engineer in Team X Factory, Nykredit" and Norlys/Nykredit .NET roles on 2026-06-12, and Nykredit is on the user's preferred-companies boost list, so those ads rank up automatically.

**Verdict:** not wired — covered indirectly via the Jobindex feeds + preferred-company boost. Revisit with a browser network-tab session if direct coverage matters later.
