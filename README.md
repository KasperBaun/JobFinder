<div align="center">

<img src="docs/screenshots/logo.svg" alt="jobfinder" width="72">

<h3>jobfinder</h3>
<p>A job-search assistant that runs only on your device.</p>
<p>
    <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
    <img src="https://img.shields.io/badge/React-19-61DAFB?style=flat-square&logo=react&logoColor=white" alt="React 19" />
    <img src="https://img.shields.io/badge/TypeScript-5-3178C6?style=flat-square&logo=typescript&logoColor=white" alt="TypeScript" />
    <img src="https://img.shields.io/badge/Vite-6-646CFF?style=flat-square&logo=vite&logoColor=white" alt="Vite" />
    <img src="https://img.shields.io/badge/LLM-LlamaSharp-000000?style=flat-square" alt="LlamaSharp" />
</p>
<p>
    <img src="https://img.shields.io/badge/local--first-✓-1E3A5F?style=flat-square" alt="Local-first" />
    <img src="https://img.shields.io/badge/no%20cloud-✓-1E3A5F?style=flat-square" alt="No cloud" />
    <img src="https://img.shields.io/badge/no%20telemetry-✓-1E3A5F?style=flat-square" alt="No telemetry" />
</p>

</div>

<p align="center">
<img src="docs/screenshots/overview.png" alt="jobfinder overview" width="820">
</p>

---

## Why

Job boards are tedious to search through. The handful of roles that actually fit you are buried under hundreds that don't.

**jobfinder** turns that into a single button. You do need to describe yourself once so jobfinder knows what you are looking for.
Your skills, your seniority, location where you'll work, your deal-breakers. You pick the sites worth checking. Then, on demand, it pulls every opening, throws out the duplicates, scores what's left against *your* profile, and hands you a short, ranked shortlist with an honest reason for each pick.

Data stays yours. No sign-up, no cloud account, no telemetry. Your skillset, your provider list, your search history, and your "good match" marks all live in a folder on your own disk.

---

## See it in action

<p align="center">
<img src="docs/screenshots/results.png" alt="Ranked results with per-match reasoning" width="820">
</p>
<p align="center"><i>One run: 801 openings pulled from 17 sources, deduped to 784, ranked to the top 25 — each with a score and a plain-English reason.</i></p>

<details>
<summary><b>Gallery</b> — the app, screen by screen</summary>
<br>
<p align="center">
<img src="docs/screenshots/sources.png" alt="Job sites / providers" width="49%">
<img src="docs/screenshots/profile.png" alt="Skillset / profile editor" width="49%">
</p>
<p align="center">
<i>Providers — toggle each job site on or off, test it, edit it.</i>
&nbsp;&nbsp;·&nbsp;&nbsp;
<i>Profile — the skillset every listing is scored against.</i>
</p>
<p align="center">
<img src="docs/screenshots/search-progress.png" alt="Live search progress" width="49%">
<img src="docs/screenshots/overview.png" alt="Overview dashboard" width="49%">
</p>
<p align="center">
<i>Search — every provider fetched live, with counts and failures shown.</i>
&nbsp;&nbsp;·&nbsp;&nbsp;
<i>Overview — sources, last run, and good matches at a glance.</i>
</p>
</details>

---

## What you get

- **Profile** Your stack, seniority, location, languages, and deal-breakers live in a single Markdown file. Edit it any time — every search uses the latest version.
- **Providers** Pick from job sites and boards (Greenhouse, The Hub, Jobindex, Remotive, SmartRecruiters, and more). Toggle each on or off; test one in isolation before you trust it.
- **A ranked shortlist on demand.** One click checks every enabled provider, removes duplicates, scores everything against your skillset, and surfaces the top matches — each with the must-have skills it hit, the nice-to-haves, and why it landed where it did.
- **A memory of every run.** Searches are kept. Look back at last Sunday's run, see which listings came up, and how many you marked as a real fit.
- **A feedback loop that learns.** Mark listings as good matches. Those signals feed back into the ranking so the next run puts more of what you liked up top.
- **An optional on-device AI judge.** Drop in a local LLM (Gemma 3 4B via LlamaSharp) to sharpen the keyword scoring with a second opinion — entirely offline. Without it, jobfinder falls back to transparent keyword ranking.

---

## Get started

Two ways in:

- **Install it.** Grab the latest self-contained installer from the
  [**releases page**](../../releases/tag/latest) — `jobfinder-setup-*.exe` for Windows and
  `jobfinder-desktop_*_amd64.deb` for Debian/Ubuntu (no .NET or Node needed). On Linux,
  `sudo apt install ./jobfinder-desktop_*_amd64.deb`; on Windows, run the `.exe`. The Windows
  build is unsigned, so SmartScreen may say "unknown publisher" — choose **More info → Run anyway**.
- **Build and run it yourself.** See [`docs/get-started.md`](docs/get-started.md) for dev mode,
  packaging as a global .NET tool, or building the installer locally.

---

- [`CHANGELOG.md`](./CHANGELOG.md) — what's shipped.

---

<div align="center">
<sub>Runs on your laptop. Stays on your laptop.</sub>
</div>
