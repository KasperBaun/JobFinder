# Jobnet.dk

**Prior:** Disabled `api` stub exists in `portals.example.yml` pointing at `job.jobnet.dk/CV/FindWork/Search`. The public-facing search no longer returns JSON unauthenticated — it serves an HTML page. Real machine integration via STAR (Styrelsen for Arbejdsmarked og Rekruttering) requires cert auth + a paid agreement (`spoc@star.dk`).

**Check:** Has STAR exposed any public open-data feed in 2026? Any change to the cert-auth requirement? Check `star.dk`, `data.gov.dk`, and the EURES export side (Norway's analogue is open-source on GitHub — DK may have something similar).

---

**Findings:**

- `https://job.jobnet.dk` 301-redirects to `https://jobnet.dk/`. The landing page is a login-gated SPA — no developer/API/RSS link, no `<link rel="alternate" type="application/rss+xml">`. The previously-stubbed `/CV/FindWork/Search` URL still returns HTML, not JSON, for unauthenticated callers. Prior assessment stands.
- STAR's official "Jobnet webservice" (JobannonceService) page (https://star.dk/digital-service/saadan-arbejder-vi-med-it-i-styrelsen/oversigt-over-digitale-platforme-for-eksterne-brugere/styrelsen-for-arbejdsmarked-og-rekrutterings-webservices-og-wiki/jobnet-webservice) confirms: free to use *the service*, but "alle nye kunder skal forvente udgifter til den egen udvikling" and requires an agreement via `spoc@star.dk`. Technical docs gated behind `starwiki.atlassian.net` (also auth-gated). No certificate-free / no-agreement path was published in 2025/2026.
- DFDG (Det Fælles Datagrundlag) webservices target municipalities and a-kasser, not the public — same `spoc@star.dk` gating.
- STAR *does* run an open-data portal at `https://www.jobindsats.dk/` (CC-BY 4.0, with a documented `api.jobindsats.dk` v2/v3). However, its scope is **labour-market statistics** (unemployment, benefit recipients, ALMP outcomes) — **not job postings**. Useless for jobfinder.
- No EURES-style open exporter found for DK (Norway's `navikt/pam-eures-stilling-eksport` on GitHub is the analogue; no DK equivalent surfaced).
- Nothing on `data.gov.dk` / `opendata.dk` exposes Jobnet vacancy data publicly in 2026.

URLs checked:
- https://jobnet.dk/
- https://job.jobnet.dk (redirects)
- https://star.dk/
- https://star.dk/digital-service/saadan-arbejder-vi-med-it-i-styrelsen/oversigt-over-digitale-platforme-for-eksterne-brugere/styrelsen-for-arbejdsmarked-og-rekrutterings-webservices-og-wiki/jobnet-webservice
- https://www.jobindsats.dk/information/

**Verdict:** `manual` — no public unauthenticated API or RSS in 2026; STAR's JobannonceService still requires an agreement (`spoc@star.dk`) and certificate auth, and `jobindsats.dk`'s open API only carries labour-market statistics, not vacancies. Existing disabled `jobnet` stub in `portals.example.yml` should stay; no enable path without a STAR agreement.

**Stub block:** _none_ (existing disabled `jobnet` block in `portals.example.yml` already documents the situation; no new block needed).
