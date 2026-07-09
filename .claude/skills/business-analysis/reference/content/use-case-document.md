# Use-Case Document Template

Every `UC0X_requirements.md` follows the same section order. Optional sections are dropped when empty — they are never included as headers with "TBD" underneath.

## Section order

| # | Section | Required | Notes |
|---|---------|----------|-------|
| 1 | `# UC0X - <Title>` | Yes | H1 only once, at the top |
| 2 | `## Overview` | Recommended | 2–4 sentences. Skip on trivial UCs. |
| 3 | `### Preceding Documents` | Optional | Bullet list pointing to overview docs or other UCs |
| 4 | `### Risks` | Optional | Bullet list of key risks |
| 5 | `## Functional Requirements` | Yes | Table — see [functional-requirements.md](functional-requirements.md) |
| 6 | `## Non-Functional Requirements` | Optional | Only for UCs with UC-specific NFRs |
| 7 | `## User Stories` | Yes | Table — see [user-stories.md](user-stories.md) |
| 8 | `## State Machine` (or `State Machines`) | If applicable | ASCII diagram + state descriptions table |
| 9 | `## Business Rules` | If applicable | `BR-NN` numbered subsections |
| 10 | `## Personas` | Optional | UC-specific persona summary — only when it adds detail beyond `overview/personas.md` |
| 11 | `## Technical Design` | Optional | ER diagram, tables, API endpoints |
| 12 | `## Acceptance Criteria` | Yes | Table — see [acceptance-criteria.md](acceptance-criteria.md) |

Separate sections with `---` on its own line.

## Minimal skeleton

```markdown
# UC0X - <Title>

## Overview

<2–4 sentences. What problem this solves and who it serves.>

**Dependencies:**
- UC0Y: <name> — <how it relates>
- <external system>

---

## Functional Requirements

| ID | Requirement | Implementation Status |
|----|-------------|------------------------|
| UC0X-FR-1 | The system must … | Implemented |

---

## User Stories

| ID | Role | I want to... | So that... | FR Reference |
|----|------|--------------|------------|--------------|
| UC0X-US1 | <Role> | <action> | <outcome> | UC0X-FR-1 |

---

## State Machine

```
Requested → InProgress → Completed
                 ↘ Cancelled
```

### State Descriptions

| State | Description | Triggered By |
|-------|-------------|--------------|

---

## Business Rules

### BR-01: <Name> (FR<N>)

- <bullet>

---

## Acceptance Criteria

| ID | Criterion | FR Reference | Verification |
|----|-----------|--------------|--------------|
| AC-01 | <observable behaviour> | UC0X-FR-1 | Unit test |

---
```

## Rules

| MUST | MUST NOT |
|------|----------|
| Lead each `## H2` directly with its table or diagram | Insert prose paragraphs between H2 and its table |
| Drop optional sections that have no rows | Write headers with "TBD" placeholders |
| Keep the document scannable in ≤ 60 seconds | Pad with marketing language |
| Cross-reference IDs verbatim | Re-state requirements in prose form |

## Related

- [functional-requirements.md](functional-requirements.md)
- [user-stories.md](user-stories.md)
- [business-rules.md](business-rules.md)
- [acceptance-criteria.md](acceptance-criteria.md)
- [../style/writing-style.md](../style/writing-style.md)
