---
name: developer-documentation
description: Use when writing or editing developer documentation — architecture overviews, subsystem guides, frontend or backend docs, deployment and infra runbooks, ADRs, or folder index pages — and when auditing wall-of-text docs or verifying documentation is complete before committing, opening a PR, or marking a task done.
---

# Developer Documentation

## Overview

Developer docs here are **short, scannable, and navigable** — a reader finds one answer in
seconds, not by reading three screens of prose. Four principles govern every doc:

1. **Structure over prose.** Tables, diagrams, and annotated code carry the meaning. Paragraphs are 1–3 sentences of glue between them.
2. **Every folder is navigable.** Each folder has an `index.md` landing page; docs link to siblings with relative paths so a reader CTRL+Clicks around.
3. **Short docs, linked.** One concept per file. When a doc runs long, split it and link — never make the reader scroll.
4. **Show, don't wall.** Any doc past the length floor earns at least one diagram or major table. Walls of text are a defect, not a style.

> **Illustrative names.** Stack names below (`React`, `Docker Compose`, `Postgres`) and folder
> names (`subsystems/`, `architecture/`) are examples — map them to your project. The full rule
> set lives in [`reference/`](reference/README.md); each file is self-contained and travels with this skill.

## When to use

- **Before writing** any developer doc — an architecture overview, subsystem guide, frontend/backend doc, deployment runbook, ADR, or folder `index.md`.
- **Before claiming done** — committing, opening a PR, or marking a task complete after touching docs.
- **When auditing** an existing doc that reads as a wall of text — see [`reference/audit-and-rewrite.md`](reference/audit-and-rewrite.md).

## What goes where

Pick the doc type, then start from its template — don't invent a structure.

| Doc type | Template |
|----------|----------|
| Folder landing / navigation page | [`templates/index-page.md`](reference/templates/index-page.md) |
| 🎯 Frontend (app / UI) | [`templates/frontend.md`](reference/templates/frontend.md) |
| 📦 Backend / architecture | [`templates/backend-architecture.md`](reference/templates/backend-architecture.md) |
| 🚀 Deployment / infra / ops | [`templates/deployment-infra.md`](reference/templates/deployment-infra.md) |
| Architecture decision | [`templates/adr.md`](reference/templates/adr.md) |
| Subsystem / feature deep-dive, CLI, API, testing | No template — apply [`house-style.md`](reference/house-style.md) + [`diagrams.md`](reference/diagrams.md) |

## Hard limits

| Element | Target | Action when exceeded |
|---------|--------|----------------------|
| Index / landing page | ≤ 60 lines | Move detail into linked leaf docs |
| Leaf doc | ≤ 250 lines | Split into linked docs, or earn the length with visuals; **> 300 lines must split or visualize** |
| Paragraph | ≤ 3 sentences | Break out a table, list, or diagram |
| Sentence | ≤ 30 words, ≤ 1 parenthetical | Split it; never stack em-dashes |
| Bullet | ≤ 2 lines | It's a table row or sub-bullets |
| Blockquote callout | 1 line | Multi-line status blockquote → a Status table |
| Any doc > 80 lines | — | Must carry ≥ 1 diagram or major table |

## Read this before writing

| Work type | Read first |
|-----------|-----------|
| Any prose / sentence / table shaping | [`reference/house-style.md`](reference/house-style.md) |
| Folder layout, index pages, links, file naming | [`reference/structure-and-navigation.md`](reference/structure-and-navigation.md) |
| Choosing or placing a diagram | [`reference/diagrams.md`](reference/diagrams.md) |
| Emojis / icons / symbols | [`reference/iconography.md`](reference/iconography.md) |
| Fixing a wall-of-text doc | [`reference/audit-and-rewrite.md`](reference/audit-and-rewrite.md) |
| A doc-type skeleton | [`reference/templates/`](reference/templates/) |

Rule index: [`reference/README.md`](reference/README.md).

## Completion checklist

Run against the docs you changed before claiming done. When a check flags a file, open it — don't skip.

| # | Check |
|---|-------|
| DOC-NAV-01 | Every folder touched has an `index.md` landing page, and every new doc is linked from it. |
| DOC-NAV-02 | Cross-links use relative paths (`./x.md`, `child/index.md`, `../sibling.md`) and every link resolves. |
| DOC-LEN-01 | Within the [hard limits](#hard-limits); any doc > 300 lines is split into linked docs or carries enough visuals to stay scannable. |
| DOC-VIS-01 | Every doc over 80 lines carries at least one diagram or major table — no long doc is unbroken prose. |
| DOC-STYLE-01 | No wall-of-text bullets (a bullet over two lines is a table row or sub-bullets) and no multi-line blockquote status header. |
| DOC-STYLE-02 | Each `## H2` leads with its table or diagram; active voice, present tense; no AI tells; sentence-case headers. |
| DOC-ICON-01 | Emojis come only from the [vocabulary](reference/iconography.md), one meaning each — no decorative emoji. |
| DOC-DIAG-01 | Mermaid for flow/sequence/state, ASCII for layers/topology; any image ships its editable source. |
| DOC-ADR-01 | Each decision is its own `adr/NNNN-title.md` with a row in `adr/index.md`; superseded ADRs are marked, never deleted. |

## Red flags — these are not exceptions

| Excuse | Reality |
|--------|---------|
| "It's just a design doc — prose is fine." | Design docs are the worst offenders. Tables and a diagram, same as any doc. |
| "The status needs a paragraph of context." | One line, or a Status table. A multi-line blockquote header is the #1 wall-of-text smell. |
| "This bullet has to explain the nuance." | A bullet over two lines is a table row or sub-bullets. Split it. |
| "The doc is long because the topic is deep." | Past 300 lines, split into linked docs or earn it with visuals. Length is not depth. |
| "I'll add the folder index later." | A folder without `index.md` is unnavigable. Add it now. |
| "More emojis make it friendlier." | Decorative emoji is noise. Use the vocabulary, one meaning each. |
| "I'll cross-link once the docs settle." | Unlinked docs are invisible. Link as you write. |

**Violating the letter of these rules is violating the spirit of them.** If a check is genuinely ambiguous, read the relevant [`reference/`](reference/README.md) file and follow it.
