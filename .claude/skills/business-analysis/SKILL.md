---
name: business-analysis
description: Use when producing or editing UML-style business-analysis documentation — use-case requirements docs, functional or non-functional requirements, user stories, business rules, acceptance criteria, personas, or state / sequence / ER diagrams — and when verifying that a use-case document is complete and traceable before committing, opening a PR, or marking the analysis done.
---

# Business Analysis

## Overview

Business-analysis documentation here is **UML-style: short, table-heavy, ID-traceable**. The
goal is a document a reader scans in minutes — not pages of generated prose. Three rules are
non-negotiable:

1. **Tables over prose.** If it can be a table row, it must be a table row.
2. **Every artefact has a stable ID.** `UC03-FR-7` survives renumbering, refactors, and reorganisation — once written, an ID is never reused or renumbered.
3. **Traceability runs both ways.** Every user story, business rule, and acceptance criterion names the FR(s) it relates to.

If you cannot meet those three, the document is not finished.

> **Illustrative names.** IDs and domain terms below (`UC03`, `Pool Partner`, `VoteCounting`,
> `FR6`) are examples — map them to your project's use cases, roles, and states. The full rule
> set lives in [`reference/`](reference/README.md); each file is self-contained and travels
> with this skill.

## When to use

- **Before writing** any business-analysis artefact — a use-case requirements doc, FR/NFR table, user story, business rule, acceptance criterion, persona, or diagram.
- **Before claiming done** — committing, opening a PR, or marking analysis complete after touching BA docs.

## Artefact router

Pick the artefact, then read its rule file before writing.

| You're capturing… | Artefact | Lives in |
|-------------------|----------|----------|
| A whole feature being analysed | Use-case doc (`UC0X_requirements.md`) | `use-cases/NN_slug/` |
| What the system must do (a capability) | Functional requirement `UC0X-FR-N` | `## Functional Requirements` table |
| A quality/constraint (security, perf…) | Non-functional requirement `NFR-N` / `NFR-UC0X-N` | `overview/requirements.md` or the UC doc |
| Who wants what and why | User story `UC0X-USN` | `## User Stories` table |
| A policy/invariant spanning FRs | Business rule `BR-NN` | `## Business Rules` |
| A verifiable done-check | Acceptance criterion `AC-NN` | `## Acceptance Criteria` table |
| A new actor or role | Persona | `overview/personas.md` (one place only) |
| An entity lifecycle (≥ 3 states) | State machine | `## State Machine` section + descriptions table |
| An ordered message workflow | Sequence diagram | `UC0X_<Topic>_SequenceDiagram.md` |
| A data model | ER diagram | inline `## Technical Design` or own `.md` |

## FR vs BR vs NFR — the boundary that gets confused

| It's an… | When |
|----------|------|
| **FR** | A top-level capability a single sentence captures: "The system must …" |
| **BR** | A policy/invariant clarifying *how* FRs behave at the edges (thresholds, separation of duties, ordering), or spanning multiple FRs. If it rewrites as a one-line FR, make it an FR and drop the BR. |
| **NFR** | A *quality* of the system (security, performance, availability), not a feature. Phrase as a measurable constraint, not an adjective. |

## ID & traceability quick reference

| Artefact | Format | Example |
|----------|--------|---------|
| Use case | `UC0X` (two-digit, zero-padded) | `UC03` |
| Functional requirement | `UC0X-FR-N` | `UC03-FR-7` |
| User story | `UC0X-USN` (no dash before N) | `UC02-US3` |
| Business rule | `BR-NN` (scoped to the doc) | `BR-02` |
| Acceptance criterion | `AC-NN` (scoped to the doc) | `AC-13` |
| NFR (system / per-UC) | `NFR-N` / `NFR-UC0X-N` | `NFR-3`, `NFR-UC04-2` |
| Cross-cutting FR | `GEN-FR-N` | `GEN-FR-1` |

```
Persona ──► UC0X         (Use-case involvement table)
UC0X-USN ──► UC0X-FR-N    (User Stories "FR Reference" column)
BR-NN     ──► UC0X-FR-N    (BR header: "### BR-02: Separation of Duties (FR11)")
AC-NN     ──► UC0X-FR-N    (Acceptance Criteria "FR Reference" column)
UC0X-FR-N ──► overview/requirements.md   (Traceability Summary)
```

Deprecated rows are marked `**[BACKLOG]**` / `**[REMOVED]**` and **kept** — never deleted, never renumbered. Full rules: [`reference/structure/id-conventions.md`](reference/structure/id-conventions.md).

## Read this before writing X

| Work type | Read first |
|-----------|-----------|
| New use-case document | [`reference/content/use-case-document.md`](reference/content/use-case-document.md) |
| Functional requirements | [`reference/content/functional-requirements.md`](reference/content/functional-requirements.md) |
| Non-functional requirements | [`reference/content/non-functional-requirements.md`](reference/content/non-functional-requirements.md) |
| User stories | [`reference/content/user-stories.md`](reference/content/user-stories.md) |
| Business rules | [`reference/content/business-rules.md`](reference/content/business-rules.md) |
| Acceptance criteria | [`reference/content/acceptance-criteria.md`](reference/content/acceptance-criteria.md) |
| Personas / actors / roles | [`reference/content/personas.md`](reference/content/personas.md) |
| State machine | [`reference/diagrams/state-machines.md`](reference/diagrams/state-machines.md) |
| Sequence diagram | [`reference/diagrams/sequence-diagrams.md`](reference/diagrams/sequence-diagrams.md) |
| ER diagram | [`reference/diagrams/er-diagrams.md`](reference/diagrams/er-diagrams.md) |
| Folder / file naming | [`reference/structure/directory-layout.md`](reference/structure/directory-layout.md) + [`file-naming.md`](reference/structure/file-naming.md) |
| ID / traceability question | [`reference/structure/id-conventions.md`](reference/structure/id-conventions.md) |
| Any prose / phrasing | [`reference/style/writing-style.md`](reference/style/writing-style.md) |

Rule index: [`reference/README.md`](reference/README.md).

## Completion checklist

Run against the BA docs you changed before claiming the work is done. When a check flags a
file, open it — don't skip.

| # | Check |
|---|-------|
| BA-DOC-01 | The UC doc follows the canonical section order; optional sections with no rows are **dropped**, not left as "TBD" headers. |
| BA-DOC-02 | Each `## H2` leads directly with its table or diagram — no intro paragraph in between. |
| BA-FR-01 | Every FR is one atomic, testable sentence starting "The system must …" (the *what*, never the *how*). |
| BA-ID-01 | Every FR, US, BR, AC, NFR has a canonical ID; none were renumbered; deprecated rows kept and marked `**[BACKLOG]**` / `**[REMOVED]**`. |
| BA-TRACE-01 | Every US, BR, and AC names the FR(s) it relates to; no orphan FR (no story / no AC) goes unexplained. |
| BA-AC-01 | Every AC is an observable outcome with exactly one standard Verification type; happy path **and** key alternates covered. |
| BA-PERSONA-01 | Each role is defined once in `overview/personas.md` and referenced by its exact name elsewhere — no per-UC role reinvention or synonyms. |
| BA-DIAG-01 | State names use one consistent PascalCase spelling across diagram, table, FRs, and code; every state machine has its **State Descriptions** table; humans are `actor` and systems are `participant` in sequence diagrams. |
| BA-STYLE-01 | No AI tells (comprehensive/robust/seamless/leverage, "in order to", self-referential intros, status/owner metadata tables); active voice, present tense. |

## Red flags — these are not exceptions

| Excuse | Reality |
|--------|---------|
| "A paragraph reads better here than a table." | If it has structure (3+ same-shaped items, MUST/MUST NOT pairs), it's a table. Tables over prose. |
| "I'll add the FR references later." | A US/BR/AC without its FR link is untraceable and fails the rule. Add it now. |
| "These IDs are messy — let me renumber to tidy the gaps." | IDs are stable forever. Mark deprecated rows, never renumber or reuse. |
| "I'll just define this role inline in the UC." | Personas live in one place. Defining a role anywhere else is a code smell. |
| "Happy-path ACs are enough to ship." | A UC with only happy-path ACs isn't done. Cover key alternates. |
| "'The system should be robust and seamless' captures it." | Unfalsifiable. State a measurable constraint, or it isn't a requirement. |
| "I'll leave the empty section with TBD as a reminder." | Drop the section. Headers with no content rot. |

**Violating the letter of these rules is violating the spirit of them.** If a check is
genuinely ambiguous, read the relevant [`reference/`](reference/README.md) file and follow it.
