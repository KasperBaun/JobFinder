# Audit & Rewrite

How to turn a wall-of-text doc set into a navigable one. Most bad docs are not bad everywhere — they
are **inconsistent**: a few solid docs next to a few dense offenders. The job is to enforce the good
patterns uniformly and break up the offenders, not to rewrite from scratch.

## Defect catalog

Each defect: how to spot it → why it fails → the fix.

| # | Symptom | Why it fails | Fix |
|---|---------|--------------|-----|
| D1 | **Multi-line blockquote "Status" header** — a `>` block of 5–18 lines before the first heading | The reader hits a wall before any structure | One-line callout, or a **Status table** ([house-style.md](house-style.md)) |
| D2 | **Bullet that is a paragraph** — a list item running 5–15 lines with nested parentheticals | Unskimmable; the bullet shape lies | Split into `**Term**: one line` sub-bullets, or a table |
| D3 | **Run-on sentence** — stacked em-dashes and parentheticals, > 30 words | The reader loses the thread mid-sentence | One idea per sentence; ≤ 1 parenthetical |
| D4 | **Long doc, no visuals** — 300+ lines of near-unbroken prose | Nothing to anchor scanning | Add a diagram/table per major section, or split the doc |
| D5 | **Missing folder `index.md`** | The folder is unreachable by CTRL+Click | Add one from [templates/index-page.md](templates/index-page.md) |
| D6 | **Redundant index description** — "Hooks doc" for `hooks.md` | The index adds no information | Summarize and quantify: "all 18 hooks, with test strategy" |
| D7 | **Prose where a table fits** — 3+ same-shaped items in sentences | Slow to compare; easy to miss one | Convert to a table |
| D8 | **Untagged or absent code fences** | No syntax highlighting; ambiguous | Language-tag every fence |

## Rewrite workflow

Work a folder at a time, top down:

```text
1. Inventory      → list every doc + line count; flag the 300+ and the index-less folders
2. Index          → add/repair index.md; one summarized row per doc
3. Per doc:
   a. Classify     → which doc type? (templates/ has the target shape)
   b. Cap length   → over 300 lines → split into linked docs
   c. De-wall      → D1/D2/D3: blockquote→table, paragraph-bullets→sub-bullets, break run-ons
   d. Tabulate     → D7: same-shaped prose → tables
   e. Visualize    → D4: every section past the floor gets a diagram or table
   f. Normalize    → icons from the vocabulary; tag code fences
   g. Relink       → fix relative cross-links; add **See:** pairs
4. Checklist      → run the SKILL.md completion checklist against each changed doc
```

## Triage order

Fix in the order that buys the most scannability per edit:

| Priority | Target | Rationale |
|----------|--------|-----------|
| 1 | Missing `index.md` (D5) | Without it the folder is invisible; cheap to add |
| 2 | The longest doc with no visuals (D4) | Biggest wall, biggest relief |
| 3 | Multi-line status blockquotes (D1) | One per design doc, mechanical to convert |
| 4 | Paragraph-bullets and run-ons (D2, D3) | Per-section grind; do alongside D4 |
| 5 | Prose-that-should-be-tables (D7) | Polishing pass |

## Don't over-correct

| Keep | Don't |
|------|-------|
| Docs already in the target style — leave them | Rewrite a clean doc for uniformity's sake |
| Existing cross-link networks — preserve them | Drop links while restructuring |
| Genuine narrative where a flow needs explaining | Force every sentence into a table |

## Related

- [README.md](README.md)
- [house-style.md](house-style.md)
- [structure-and-navigation.md](structure-and-navigation.md)
- [diagrams.md](diagrams.md)
