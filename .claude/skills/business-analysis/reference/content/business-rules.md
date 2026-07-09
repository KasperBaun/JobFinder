# Business Rules

Business rules (BRs) capture **policies, constraints, and invariants** that span multiple FRs or that need clarification beyond a one-line FR. They live inside the use-case document, under `## Business Rules`.

## Format

Each BR is a small subsection — header with ID and FR reference, then short bullets.

```markdown
### BR-01: Approval Threshold (FR6)
- Required approval percentage: **>= 66%** of voters (pool partners)
- Counting method: By distinct pool partner (not individual votes)
- Only approved votes before ResultApprovedOn timestamp are counted

### BR-02: Separation of Duties (FR11)
- The user/process that initiates a vote **cannot** conclude it
- Only users with **Vote Concluder** role can transition VoteCounting → VoteCompleted
```

| Element | Rule |
|---------|------|
| ID | `BR-NN` — scoped to the document, sequential, never renumbered |
| Header | `### BR-NN: <Short Name> (FR<N>[, FR<N>])` |
| Body | 2–5 short bullets; bold key thresholds, role names, and outcomes |
| Length | Aim for ≤ 6 bullets; if longer, split into two BRs |

## Rules

| MUST | MUST NOT |
|------|----------|
| Reference the FRs the rule clarifies in the header | Define brand-new behaviour the FRs don't already imply |
| Use bullets, not paragraphs | Write essay-style explanations |
| State the **rule**, not the **rationale** | "We do this because historically …" — that belongs in `## Overview` |
| Name a single concrete policy | Bundle five unrelated rules under one BR |
| Bold the load-bearing values (thresholds, role names) | Leave critical numbers buried in prose |

## When to use a BR vs an FR

| Use BR when … | Use FR when … |
|---------------|---------------|
| The rule clarifies *how* an FR behaves at edges (thresholds, separations of duty, ordering) | The rule is a top-level capability |
| The rule spans multiple FRs | A single sentence captures it |
| The rule is a constraint or invariant | The rule describes an action |

If you can rewrite the BR as a one-line FR, do so and drop the BR.

## Good vs bad

```markdown
✅ ### BR-05: Vote Code Security (FR4)
   - Each vote request contains a **unique VoteCode (Guid)**
   - Only the intended voter can cast the vote via their personalized link
   - Vote links include voter email for validation

❌ ### BR-05: Security
   We will be very careful about security and make sure that everything is secure throughout
   the voting process. This includes various measures that the engineering team will define.
```

## Related

- [functional-requirements.md](functional-requirements.md)
- [acceptance-criteria.md](acceptance-criteria.md)
- [../structure/id-conventions.md](../structure/id-conventions.md)
