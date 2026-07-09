# Business Analysis Rules

Full rule set for **UML-style business-analysis documentation** — short, table-heavy,
ID-traceable. These rules exist so an agent (or human) can sit down and produce
documentation a reader scans in minutes, not pages of generated prose.

Names like `UC03`, `Pool Partner`, and `VoteCounting` are **illustrative** — map them to
your project's use cases, roles, and states.

---

## Reading order

1. [`structure/directory-layout.md`](structure/directory-layout.md) — folder tree
2. [`structure/file-naming.md`](structure/file-naming.md) — file and folder names
3. [`structure/id-conventions.md`](structure/id-conventions.md) — IDs for FRs, USs, BRs, ACs, NFRs
4. [`content/use-case-document.md`](content/use-case-document.md) — section template for `UC0X_requirements.md`
5. The artefact rules below — one per artefact type

---

## Rule index

| Topic | File | What it covers |
|-------|------|----------------|
| Folder tree | [structure/directory-layout.md](structure/directory-layout.md) | What lives under `business-analysis/` |
| File naming | [structure/file-naming.md](structure/file-naming.md) | `UC03_requirements.md`, `NN_kebab-case` folders |
| ID conventions | [structure/id-conventions.md](structure/id-conventions.md) | `UC03-FR-7`, `BR-02`, `AC-13`, traceability |
| Use-case doc | [content/use-case-document.md](content/use-case-document.md) | Section order and required tables |
| FRs | [content/functional-requirements.md](content/functional-requirements.md) | "The system must …" voice, atomic, testable |
| NFRs | [content/non-functional-requirements.md](content/non-functional-requirements.md) | Security, performance, etc. — one row each |
| User stories | [content/user-stories.md](content/user-stories.md) | Role / I want / So that, linked to FRs |
| Business rules | [content/business-rules.md](content/business-rules.md) | `BR-NN` numbered, FR-referenced |
| Acceptance criteria | [content/acceptance-criteria.md](content/acceptance-criteria.md) | `AC-NN`, FR ref + verification type |
| Personas | [content/personas.md](content/personas.md) | Internal/external split, capabilities tables |
| State machines | [diagrams/state-machines.md](diagrams/state-machines.md) | ASCII state diagrams + descriptions table |
| Sequence diagrams | [diagrams/sequence-diagrams.md](diagrams/sequence-diagrams.md) | Mermaid `sequenceDiagram` with `alt` / `loop` |
| ER diagrams | [diagrams/er-diagrams.md](diagrams/er-diagrams.md) | Mermaid `erDiagram` for data models |
| Writing style | [style/writing-style.md](style/writing-style.md) | Brevity, voice, tables-first, AI tells to avoid |

---

## Quick decision

```
New feature being analysed?            → Create use-case folder, write UC0X_requirements.md
Cross-cutting requirement?             → Add row to overview/requirements.md
New actor or role?                     → Update overview/personas.md
Workflow / process flow?               → Add sequence diagram .md alongside requirements
Lifecycle / state transitions?         → Add State Machine section inside requirements
Data model for a use case?             → Add ER diagram .md alongside requirements
```

---

## The three non-negotiables

1. **Tables over prose.** If it can be a table row, it must be a table row.
2. **Every artefact has a stable ID.** `UC03-FR-7` survives renumbering, refactors, and reorganisation.
3. **Traceability runs forward and backward.** Every US, BR, and AC names the FR(s) it relates to.

If you cannot meet those three, the document is not finished.
