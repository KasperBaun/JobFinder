# Maersk / A.P. Moller - Maersk (favorite-company career site)

**Prior:** User-declared preferred employer (R-091). Large Copenhagen tech hub.

**Findings:**

- ATS is Workday, tenant `maersk`, host `wd3`. Site name is **`Maersk_Careers`** — `/en-US/Maersk` 404s; a second site `PT_Careers` exists but holds only ~6 roles (ignored).
- Public CXS JSON endpoint, no auth: `POST https://maersk.wd3.myworkdayjobs.com/wday/cxs/maersk/Maersk_Careers/jobs` with `searchText: "software engineer"` → ~69 roles globally (2026-06-12).
- Same Workday caveats as LEGO: relative `postedOn` (null date), JSON-LD descriptions via enrichBody.

**Verdict:** `api` — wired as catalog id 39 `workday-maersk`.
