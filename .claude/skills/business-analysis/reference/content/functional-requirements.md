# Functional Requirements

A functional requirement (FR) is a single, testable statement of what the system must do. One row, one ID, one sentence — that is the bar.

## Table format

```markdown
| ID          | Requirement | Implementation Status |
|-------------|-------------|------------------------|
| UC0X-FR-1   | The system must … | Implemented |
| UC0X-FR-2   | The system must … | **BACKLOG**  |
```

| Column | Required | Notes |
|--------|----------|-------|
| ID | Yes | `UC0X-FR-N`, sequential, never renumbered |
| Requirement | Yes | One sentence, starts with "The system must …" |
| Implementation Status | When tracking delivery | `Implemented`, `**BACKLOG**`, `**REMOVED**` |

Drop the third column entirely on freshly-written specs where status isn't relevant yet.

## Voice and shape

| MUST | MUST NOT |
|------|----------|
| Start with **"The system must …"** (or "shall") | "We will build a feature that…" |
| Be atomic — one capability per row | Pack 3 capabilities into one row |
| Be testable — a reviewer can imagine a pass/fail check | Use vague verbs ("support", "enable", "improve") without a measurable outcome |
| Reference other FRs by ID, in parentheses | Re-state another FR in different words |
| State the **what**, not the **how** | Specify implementation (DB tables, APIs, code) |
| Stay under ~ 40 words per row | Spill into multi-paragraph descriptions |

## Good vs bad

```markdown
✅ | UC03-FR-2 | The system must check if the partner's company is already an active member of the
              specified pool. If not, the system must automatically initiate a PoolMembershipApplication
              (see UC02-FR-1 to UC02-FR-7) before the vessel's onboarding can proceed. |

❌ | UC03-FR-X | We should make sure that vessel onboarding works smoothly and handles all the edge
              cases around pool membership, ideally by integrating with the membership module. |
```

The bad version is unfalsifiable. The good version names the precondition, the action, and the cross-reference.

## Backlog rows

Keep the ID, mark the status, leave the text intact:

```markdown
| UC02-FR-14 | Reinstatement: If an offboarded Pool Partner requests to regain access … | **BACKLOG** |
```

Adding `**[BACKLOG]**` to the text itself is also acceptable when there is no status column.

## Where FRs live

- **UC-specific FRs** → `use-cases/NN_slug/UC0X_requirements.md`
- **System-wide / cross-cutting FRs** → `overview/requirements.md` (as `GEN-FR-N`)
- **NFRs** → see [non-functional-requirements.md](non-functional-requirements.md)

## Related

- [../structure/id-conventions.md](../structure/id-conventions.md)
- [user-stories.md](user-stories.md)
- [acceptance-criteria.md](acceptance-criteria.md)
- [business-rules.md](business-rules.md)
