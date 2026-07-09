# ID Conventions

Every artefact has an ID. IDs are **stable** — once written, they are not renumbered, even if the artefact is deleted, deprecated, or reordered.

## ID formats

| Artefact | Format | Example |
|----------|--------|---------|
| Use case | `UC0X` (two-digit, zero-padded) | `UC03` |
| Functional requirement | `UC0X-FR-N` | `UC03-FR-7` |
| User story | `UC0X-USN` (no dash between US and N) | `UC02-US3` |
| Business rule | `BR-NN` (scoped within the doc) | `BR-02` |
| Acceptance criterion | `AC-NN` (scoped within the doc) | `AC-13` |
| Non-functional requirement (system-wide) | `NFR-N` | `NFR-3` |
| Non-functional requirement (per-UC) | `NFR-UC0X-N` | `NFR-UC04-2` |
| Cross-cutting / general FR | `GEN-FR-N` | `GEN-FR-1` |

## Rules

| MUST | MUST NOT |
|------|----------|
| Allocate IDs sequentially in the order written | Renumber to "tidy" gaps |
| Mark deprecated rows `**[BACKLOG]**` or `**[REMOVED]**` and keep the ID | Delete rows and shift numbers |
| Use exactly the canonical separator (`-`, `_`) above | Invent variants (`UC03_FR_7`, `UC03.FR.7`) |
| Reference IDs verbatim in linked artefacts | Paraphrase ("see the seventh FR") |

## Traceability — what links to what

```
Persona ──► UC0X         (Use-case involvement table)
UC0X-USN ──► UC0X-FR-N   (User Stories table, "FR Reference" column)
BR-NN     ──► UC0X-FR-N   (Header line: "### BR-02: Separation of Duties (FR11)")
AC-NN     ──► UC0X-FR-N   (Acceptance Criteria table, "FR Reference" column)
UC0X-FR-N ──► overview/requirements.md   (Traceability Summary table)
```

## Backlog and removal handling

Keep the row, mark it inline:

```markdown
| UC02-FR-14 | Reinstatement: …                            | **BACKLOG**  |
| UC02-FR-15 | The evaluation is based on the most recent…  | **BACKLOG**  |
```

For removal, change Implementation Status to `**REMOVED**` and append a one-line reason — never delete the row, never reuse the ID.

## Cross-document references

When citing across docs, include the full ID:

> See `UC02-FR-7` and `UC02-FR-8` for downstream activation behaviour.

Within a doc, an abbreviated form is acceptable inside Business Rules headers:

> ### BR-01: Approval Threshold (FR6)

…where `FR6` is short for `UC02-FR-6` because the document is `UC02_requirements.md`.

## Related

- [../content/functional-requirements.md](../content/functional-requirements.md)
- [../content/acceptance-criteria.md](../content/acceptance-criteria.md)
- [../content/business-rules.md](../content/business-rules.md)
