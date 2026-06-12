# H1 (h1.co) (favorite-company career site)

**Prior:** User-declared preferred employer (R-091); user confirmed the company is https://h1.co/ (healthcare-data platform). Remote-friendly with Copenhagen presence.

**Findings:**

- Careers page `h1.co/company/careers/` links to Lever (`jobs.lever.co`).
- Lever public postings API, slug `h1`: `GET https://api.lever.co/v0/postings/h1?mode=json` → bare JSON array, 20 postings (2026-06-12), including a Copenhagen hybrid "AI Scientist" role.
- Clean field mapping: `text` (title), `hostedUrl` (url), `categories.location`, `descriptionPlain` (full plain-text description inline — no enrichment needed).
- `createdAt` is epoch milliseconds — `DateTimeOffset.TryParse` ignores it, posting date stays null. Acceptable.
- Response root IS the array → omit `items_path` (JsonValueReader.Walk returns root for null path).

**Verdict:** `api` — wired as catalog id 40 `lever-h1`.
