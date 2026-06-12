# LEGO (favorite-company career site)

**Prior:** User-declared preferred employer (R-091). Global toy maker; engineering hub in Billund, some Copenhagen roles.

**Findings:**

- ATS is Workday, tenant `lego`, host `wd103`, site `LEGO_External`.
- Public CXS JSON endpoint, no auth: `POST https://lego.wd103.myworkdayjobs.com/wday/cxs/lego/LEGO_External/jobs` with flat body `{"searchText":"software","limit":20,"offset":0}` → 200, ~39 software roles (2026-06-12).
- `postedOn` is relative text ("Posted Today") — posting date unparseable, stays null.
- Some items omit `locationsText` (location only in `bulletFields[0]`) — those listings keep a null location.
- Job pages are a JS shell **but** embed the posting in JSON-LD, which body enrichment's tag-stripper preserves — descriptions work via `enrichBody: true`.
- Job URL pattern verified: `https://lego.wd103.myworkdayjobs.com/en-US/LEGO_External{externalPath}` → HTTP 200.

**Verdict:** `api` — wired as catalog id 37 `workday-lego` (POST + flat bodyTemplate + offset/limit pagination + enrichBody).
