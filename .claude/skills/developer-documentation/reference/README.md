# Developer Documentation Rules

Full rule set for **developer documentation** — short, scannable, navigable. These rules exist
so an agent (or human) can produce docs a reader skims in seconds, not screens of generated prose.

Stack and folder names below are **illustrative** — map them to your project.

---

## Reading order

1. [`house-style.md`](house-style.md) — how the prose reads: tables-first, sentence shape, AI tells
2. [`structure-and-navigation.md`](structure-and-navigation.md) — folder layout, `index.md` pattern, links, naming
3. [`iconography.md`](iconography.md) — the controlled emoji/symbol vocabulary
4. [`diagrams.md`](diagrams.md) — which diagram, when, and how
5. [`audit-and-rewrite.md`](audit-and-rewrite.md) — defect catalog + the rewrite workflow
6. [`templates/`](templates/) — copy-paste skeletons, one per doc type

---

## Rule index

| Topic | File | What it covers |
|-------|------|----------------|
| House style | [house-style.md](house-style.md) | Tables-first, `**Term**: explanation` bullets, sentence shape, length caps, AI tells |
| Structure & navigation | [structure-and-navigation.md](structure-and-navigation.md) | Folder tree, `index.md` skeleton, relative links, kebab-case naming |
| Iconography | [iconography.md](iconography.md) | The fixed semantic emoji set, one meaning each |
| Diagrams | [diagrams.md](diagrams.md) | Mermaid for flow, ASCII for layers, images for branded diagrams |
| Audit & rewrite | [audit-and-rewrite.md](audit-and-rewrite.md) | Defect catalog (symptom → fix) + folder rewrite workflow |
| Index page template | [templates/index-page.md](templates/index-page.md) | Folder landing page skeleton |
| Frontend template | [templates/frontend.md](templates/frontend.md) | Framework, state, styling, component map, flows |
| Backend template | [templates/backend-architecture.md](templates/backend-architecture.md) | Layers/modules, request flow, boundaries |
| Deployment template | [templates/deployment-infra.md](templates/deployment-infra.md) | Pipelines, containers, envs, networks, runbook |
| ADR template | [templates/adr.md](templates/adr.md) | One-file-per-decision, MADR-lite |

---

## Quick decision

```
Writing a new doc?                     → Start from templates/<type>.md
Adding a doc to a folder?              → Add a row to that folder's index.md
No index.md in the folder?             → Create one from templates/index-page.md
Doc over 300 lines?                    → Split into linked docs, or add visuals
Recording a decision with impact?      → Create adr/NNNN-title.md + index.md row
Fixing a wall-of-text doc?             → Follow audit-and-rewrite.md
Unsure which diagram?                  → diagrams.md picks by content type
```

---

## The four principles

1. **Structure over prose.** If it has structure, it's a table, list, or diagram — not a paragraph.
2. **Every folder is navigable.** An `index.md` landing page and relative cross-links, always.
3. **Short docs, linked.** One concept per file; split rather than scroll.
4. **Show, don't wall.** Any doc past the length floor earns a diagram or major table.

If you cannot meet those four, the doc is not finished.
