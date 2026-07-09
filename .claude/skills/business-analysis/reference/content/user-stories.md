# User Stories

User stories describe **who** wants **what** and **why**, linked back to the FRs that satisfy them. Same row, same table, every use case.

## Table format

```markdown
| ID | Role | I want to... | So that... | FR Reference |
|----|------|--------------|------------|--------------|
| UC02-US1 | Pool Partner | Apply for membership in a pool | My company can participate in a vessel pool | UC02-FR-1 |
| UC02-US3 | Voter (Pool Member) | Receive a vote request with unique link | I can approve/reject new member applications | UC02-FR-3, UC02-FR-4 |
```

| Column | Required | Notes |
|--------|----------|-------|
| ID | Yes | `UC0X-USN` (no dash between US and N) |
| Role | Yes | Persona name from `overview/personas.md`, or `System` for automated stories |
| I want to... | Yes | A concrete action, present-tense verb |
| So that... | Yes | The outcome / motivation — never "it works" |
| FR Reference | Yes when FRs exist | Comma-separated `UC0X-FR-N` IDs |

## Voice

> As a `<Role>`, I want to `<action>` so that `<outcome>`.

The table compresses the classic narrative form — never re-expand it into prose paragraphs above the table.

## Rules

| MUST | MUST NOT |
|------|----------|
| Use roles that exist in `overview/personas.md` | Invent ad-hoc role names per use case |
| Link to at least one FR (or explain why none yet exists) | Leave the FR Reference column blank silently |
| Allow `System` as a role for automated behaviour | Write `System` stories for human actions |
| Keep the "I want to…" clause concrete | "I want the system to be good" |
| Keep one story per row | Bundle "I want A and B and C" |
| Cover both happy path and key alternates (e.g. an Admin viewer story) | Write only the primary user's story |

## Good vs bad

```
✅ | UC03-US4  | Signee            | Digitally sign the PoolContract            | Approve the vessel's participation   | UC03-FR-7 |
✅ | UC02-US5  | System            | Track approval rate and notify when threshold reached | Vote Concluders can conclude eligible applications | UC02-FR-6, UC02-FR-12 |

❌ | UC03-US?  | User              | Use the app                                | It works                              |           |
❌ | UC02-US?  | Pool Partner User | Apply, vote, review, sign, and finalize everything related to membership | Things happen | UC02-FR-1 to UC02-FR-13 |
```

## Coverage check

After writing the stories table, sanity-check coverage:

- Every primary FR should be referenced by at least one user story.
- Every persona that appears in the use case should drive at least one story.
- Any orphaned FR (no story) is a flag — either add a story or justify in the Overview.

## Related

- [functional-requirements.md](functional-requirements.md)
- [personas.md](personas.md)
- [acceptance-criteria.md](acceptance-criteria.md)
