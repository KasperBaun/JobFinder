# TechJob.dk

**Prior:** Ex-Jobfinder, rebranded by Teknologiens Mediehus (Ingeniøren). ~450 active roles. Brand was relaunched recently — feed URL may have moved.

**Check:** Current feed URL? Try `techjob.dk/rss`, `/feed`. Check `ing.dk` / `ingeniøren.dk` for shared backend. Watch network tab on the search page.

---

**Findings:**

- Rebrand confirmed: `https://jobfinder.dk` and `https://jobfinder.dk/rss` both `301` to `https://techjob.dk/` (and `/rss`). No legacy feed survives.
- `https://techjob.dk/rss` exists and is a well-formed RSS 2.0 document, but it is the **articles** channel ("TechJob articles") and is currently empty. `https://techjob.dk/en/rss` is the same — empty articles channel, lang=en. Neither carries job postings.
- Tried `/feed`, `/jobs.rss`, `/jobs/rss`, `/job/rss` on `techjob.dk` — all `404`. Same paths on `ing.dk` — all `404` except `ing.dk/rss`, which is the Ingeniøren editorial feed (news articles, not jobs).
- Tried `/api/jobs`, `/api/v1/jobs`, `/jsonapi`, `/jsonapi/node/job` — all `404`. No public JSON API surfaced.
- Site is Drupal (robots.txt mentions `/admin/`, `/node/add/`, `/core/`, `/profiles/`, `/index.php/admin/`). Search lives at `/jobs?page=N&category[ID]=ID`; individual jobs at `/job/<slug>-<numeric-id>`. Page is server-rendered — no JSON XHR observed in the markup.
- `https://techjob.dk/sitemap.xml` is a flat sitemap listing every active job (~400 entries, DA + EN parallels) with `lastmod`. Stable, machine-readable — but it's the only structured surface.
- `robots.txt` ships an explicit `Disallow: /` for `ClaudeBot`, `Claude-Web`, `anthropic-ai`, `cohere-ai`, and ~40 other AI/LLM crawlers. The site is actively opting out of automated ingestion.
- No partner / affiliate / developer programme advertised by Teknologiens Mediehus. Job-posting sales contact is `techjob.dk/kontakt-jobsalg`; commercial relationship would have to be negotiated.
- WebSearch on "techjob.dk rss", "techjob.dk api", "jobfinder.dk feed", "Teknologiens Mediehus jobs feed" returned no third-party references to a jobs feed or API — only the company's own marketing pages.

**Verdict:** `manual`

Rationale: no public RSS/API for jobs, only an empty articles feed. The sitemap would technically permit a scrape, but the robots.txt is an explicit opt-out (and CLAUDE.md forbids anti-bot bypass). Ship as a `manual` provider with the search URL the user can open; revisit if Teknologiens Mediehus opens a partner feed.

**Stub block:** _n/a (manual — no api/rss block to add)_

Suggested `manual` entry for `src/config/portals.example.yml` when seeded:

```yaml
- name: techjob
  type: manual
  url: https://techjob.dk/jobs
  notes: |
    Teknologiens Mediehus (Ingeniøren). Rebranded from jobfinder.dk in
    2024 — old domain 301s here. ~400 engineering / IT roles, mostly DK.
    No public jobs RSS or JSON API as of 2026-05; /rss is an empty
    articles channel. Sitemap.xml lists every job but robots.txt
    explicitly blocks ClaudeBot / anthropic-ai / 40+ AI crawlers, so
    automated ingestion is out per CLAUDE.md ("no anti-bot bypassing").
    Revisit if a partner feed appears at techjob.dk/kontakt-jobsalg.
```
