# Danske Bank (favorite-company career site)

**Prior:** User-declared preferred employer (R-091). Major DK bank, large Copenhagen engineering org.

**Findings:**

- ATS is Oracle Recruiting Cloud: job links on `danskebank.com/careers` point at `https://ejqi.fa.ocs.oraclecloud.eu/hcmUI/CandidateExperience/en/sites/CX_1001/job/{id}`.
- The anonymous REST endpoint answers without auth: `GET https://ejqi.fa.ocs.oraclecloud.eu/hcmRestApi/resources/latest/recruitingCEJobRequisitions?onlyData=true&expand=requisitionList.secondaryLocations&finder=findReqs;siteNumber=CX_1001,limit=N,sortBy=POSTING_DATES_DESC` → 145 open requisitions, real fields (`Id`, `Title`, `PostedDate` as ISO date, `PrimaryLocation`). The `expand=requisitionList.secondaryLocations` param is REQUIRED — without it `requisitionList` comes back empty.
- **Blocker:** the requisitions live at `items[0].requisitionList` — `JsonValueReader.Walk` only traverses object properties, not array indices, so `items_path: "items.0.requisitionList"` can't resolve. Wiring this needs a small adapter extension (numeric path segments in `Walk`), which was explicitly out of scope for this batch.

**Verdict:** `api` — UNBLOCKED 2026-06-12 (later): `JsonValueReader.Walk` learned numeric path segments (array indexing), wired as catalog id 44 `oracle-danskebank`. Live provider test: 100 listings. Oracle tolerates percent-encoded `finder` params; job pages carry extractable text for body enrichment.
