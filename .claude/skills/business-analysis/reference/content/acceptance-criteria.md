# Acceptance Criteria

Acceptance criteria (ACs) are the **verifiable checks** that decide whether a use case is done. Each AC names the FR it satisfies and the type of test that proves it.

## Table format

```markdown
| ID | Criterion | FR Reference | Verification |
|----|-----------|--------------|--------------|
| AC-01 | Partner can submit membership application | FR1 | Integration test |
| AC-06 | Approval rate calculated correctly (by partner, not votes) | FR6 | Unit test |
| AC-11 | Application state always visible to users | FR10 | UI test |
```

| Column | Required | Notes |
|--------|----------|-------|
| ID | Yes | `AC-NN`, scoped to the document |
| Criterion | Yes | Short, observable behaviour |
| FR Reference | Yes | `FRN` shorthand acceptable inside the same UC doc |
| Verification | Yes | One of the standard types below |

## Verification types

| Type | Use when |
|------|----------|
| **Unit test** | Pure logic, calculation, validation — no I/O |
| **Integration test** | Endpoint-level, DB or external system involved |
| **UI test** | Browser-driven flow, frontend behaviour |
| **Manual test** | One-off acceptance step that cannot be automated yet |
| **Audit / log inspection** | Verifying audit trails, side-effects on logs |

Pick exactly one. If multiple, choose the lowest-cost type that actually proves the criterion.

## Rules

| MUST | MUST NOT |
|------|----------|
| Phrase each AC as an observable outcome | "The code is well-written" |
| Map every AC to ≥ 1 FR | Have orphan ACs with no FR Reference |
| Use one of the standard Verification types | Invent ad-hoc types per row |
| Keep each row to one line | Multi-paragraph ACs |
| Cover both happy path and key alternates | Ship a UC with only happy-path ACs |

## Good vs bad

```
✅ | AC-04  | Voters can approve via unique vote link without login | FR4 | Integration test |
✅ | AC-13  | Application auto-approved when pool has zero active members | FR17 | Integration test |

❌ | AC-X   | The voting process is robust and user-friendly | – | – |
```

## Coverage check

After writing the ACs:

- Every FR should appear in at least one AC.
- Every BR with externally observable behaviour should map to at least one AC.
- The set of ACs should be enough to declare the use case **done** — no implicit "we'll know it when we see it".

## Related

- [functional-requirements.md](functional-requirements.md)
- [business-rules.md](business-rules.md)
- [user-stories.md](user-stories.md)
