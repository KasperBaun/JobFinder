# Structure & Navigation

Docs are a **navigable tree**, not a pile of files. Every folder has a landing page, every doc
links to its neighbours, and a reader CTRL+Clicks from the root to any leaf without searching.

## Folder layout

```text
documentation/
├── index.md                  # root landing page — links every section
├── architecture/
│   ├── index.md              # section landing page
│   ├── overview.md
│   └── adr/
│       ├── index.md          # ADR log — one row per decision
│       └── 0001-modular-monolith.md
├── frontend/
│   ├── index.md
│   └── state-management.md
├── backend/
│   └── index.md
└── deployment/
    └── index.md
```

| Rule | Detail |
|------|--------|
| Every folder has `index.md` | It is the only entry point a reader needs for that folder. |
| One concept per leaf file | Split when a file covers two topics; link them. |
| `_`-prefixed = hidden | `_archive/`, `_drafts/` are legacy/WIP, skipped by readers. |
| Assets beside their source | `domain-model.png` ships with `domain-model.drawio`. |

## The index.md skeleton

Every landing page follows the same shape — see [`templates/index-page.md`](templates/index-page.md):

```markdown
# <Folder Title>

<One sentence: what this folder documents.>

## 📁 <Section>

| Doc | Covers |
|-----|--------|
| [Overview](./overview.md) | The 5-layer request flow and layer responsibilities |
| [ADRs](./adr/index.md) | 9 architecture decisions, newest first |
```

| Rule | Why |
|------|-----|
| H1 → one-sentence purpose → `## 📁 Section` → 2-column link table | Predictable; a reader learns the shape once. |
| The "Covers" column **summarizes**, not repeats the title | "all 18 hooks documented" beats "Hooks doc". |
| Quantify where you can | "9 decisions", "13 patterns" — sets expectations. |
| ≤ 60 lines | A landing page routes; it does not explain. |

## Linking

| From → to | Link |
|-----------|------|
| Same folder | `[Overview](./overview.md)` |
| Child folder | `[ADRs](./adr/index.md)` |
| Parent / sibling | `[App Settings](../deployment/app-settings.md)` |
| Cross-ref at end of a leaf doc | `**See:** [Audio I/O](./audio-io.md)` |

| Rule | Detail |
|------|--------|
| Relative paths only | `./`, `../`, `child/` — never absolute disk paths. |
| Every link resolves | A dead link is a broken doc. Check on save. |
| Link as you write | Unlinked docs are invisible; don't defer linking. |
| Bidirectional for tight pairs | If A "sits in front of" B, each links the other. |

## Naming

| Thing | Convention | Example |
|-------|------------|---------|
| Doc file | kebab-case `.md` | `state-management.md` |
| Landing page | always `index.md` | `architecture/index.md` |
| ADR file | `NNNN-kebab-title.md` | `0003-postgres-over-mongo.md` |
| Folder | kebab-case | `deployment/`, `meeting-copilot/` |
| Hidden/legacy | leading `_` | `_archive/` |
| Heading | sentence case, `📁` only on index sections | `## Request flow` |

## Related

- [README.md](README.md)
- [house-style.md](house-style.md)
- [iconography.md](iconography.md)
- [templates/index-page.md](templates/index-page.md)
