# House Style

How the prose reads — the difference between docs a developer skims and docs a developer closes.
These rules apply to **every** doc type, including ones with no template (CLI, API, testing, telemetry).

## The four commandments

1. **Teach with prose; tabulate reference data.** Short, declarative sentences explain *how* it works and *why* a choice was made; tables hold the reference data (commands, config, comparisons). Don't shred an explanation into table cells.
2. **Short. Then shorter.** Same point, fewer words. Past 30 words, split the sentence.
3. **Active voice, present tense.** "The backend sends the reply." Not "The reply will be sent by the backend."
4. **Every section earns its prose.** If a table or diagram says it better, delete the paragraph.

## Sentence shape

| MUST | MUST NOT |
|------|----------|
| One idea per sentence, subject front-loaded | Stack three clauses behind em-dashes and parentheticals |
| ≤ 1 parenthetical per sentence | "(api straddles all three nets; the engine sits on `egress` only — it cannot reach…)" |
| Bold key thresholds, names, outcomes | Leave critical values floating in plain text |
| Break a long sentence into two | Run a "sentence" across four lines |

## The default bullet shape

Use `**Term**: explanation` — the term scannable in bold, the explanation one line:

```markdown
- **Decoupling**: workflow logic stays out of the request handler.
- **Idempotency**: a retried message produces no duplicate side effect.
```

A bullet that runs past **two lines** is not a bullet — it's a table row or a set of sub-bullets. Split it.

## Tables-first reflex

Convert prose to a table whenever there are:

- 3+ items with the same shape
- 2+ attributes per item
- Pairs of MUST/MUST NOT, IF/THEN, choice/why, input/output

Two-column comparison tables (✅ Good vs ❌ Bad, Use vs Don't) are the highest-leverage device — use them liberally. But this reflex is for **reference data**, not explanation — don't tabulate a mechanism that needs teaching (see below).

## Teach, don't just tabulate

The fastest way to make a doc unreadable is to turn every paragraph into a table. Tables are for data a reader **looks up**; prose is for understanding a reader **builds**. A good doc reads like an engineer explaining the system — narrative that ties the parts together, with tables and diagrams as the reference material beside it.

| Reach for a table | Reach for prose |
|-------------------|-----------------|
| Commands, flags, exit codes | How a mechanism works, end to end |
| Config keys + defaults | Why a design choice was made over the alternative |
| Option A vs B vs C | The one non-obvious gotcha worth teaching |
| Field → meaning | The thread that connects the pieces |

When in doubt, ask: could a new engineer *learn* the system from this doc, or only *audit* it? If only audit, the prose got stripped — add it back.

## Document what exists, not status

Developer docs describe the system as it is now. They are not a status tracker, a changelog, or a backlog. **Delete on sight:**

| Cut | Why |
|-----|-----|
| "Future work", "Not built yet", "Still to come", "Open items", "TODO" | The issue tracker owns the backlog; a doc listing unbuilt work rots the day priorities change |
| done / deferred / pending columns; "✅ shipped / ❌ pending" | Git owns what shipped when — describe the behaviour that exists |
| "Resolved decisions", "verified on a clean run", changelog brags | State the decision as the current design; drop the diary |
| Roadmap / phase tables for work that already shipped | If it shipped, document the behaviour — not the plan that delivered it |

A genuine, current limitation can stay — stated once as a property of the system ("the engine has direct internet, so exfil is possible"), never as a roadmap item ("egress filtering: planned").

## Callouts

One-line blockquotes with a bold label. Never a multi-line status blockquote (the #1 wall-of-text smell).

```markdown
> ⚠️ **Caveat:** the mic reopens for ~7s; on headphones it hears only silence.
> 🔒 **Security:** the engine network has no route to Postgres.
```

If the "status" needs more than one line, it's a **Status table**, not a blockquote:

| Tier | Default | Contains |
|------|---------|----------|
| 0 | yes | stock Docker, three networks |
| 1 | on gVisor host | + enforced egress allowlist |

## Length caps

| Element | Target | Hard cap |
|---------|--------|----------|
| Index / landing page | ≤ 40 lines | 60 |
| Leaf doc | ≤ 250 lines | 300 (split or visualize past this) |
| Paragraph | 1–3 sentences | 3 |
| Sentence | ≤ 25 words | 30 |
| Bullet | 1 line | 2 lines |

If you cannot meet the cap, split into two docs or convert to a table.

## AI tells to delete on sight

| Tell | Replacement |
|------|-------------|
| "Comprehensive", "robust", "seamless", "leverage" | Cut, or replace with a measurable fact |
| "In order to …" | "to …" |
| "It is important to note that …" | Delete the phrase, keep the fact |
| "Various", "several", "a number of" | A concrete number, or omit |
| Self-referential intros ("This document is…", "Single source of truth for…") | Cut. Filename and folder convey purpose. |
| Multi-paragraph explanation of a one-row table | Delete the paragraphs |
| Section header with no content under it | Delete the section |

## Layout rules

| MUST | MUST NOT |
|------|----------|
| Lead each `## H2` directly with its table or diagram | Insert an intro paragraph above every table |
| Separate top-level sections with `---` | Rely on blank lines alone |
| Code-fence and language-tag every block (` ```bash `, ` ```csharp `, ` ```text `) | Leave fences untagged |
| Use sentence-case headers | TITLE CASE / Random Capitalization |
| Cross-reference with `**See:**` + a relative link | Re-explain a sibling doc inline |
| Keep to H2/H3/H4 | Go to H5/H6 |

## Brevity test

Before committing a section, ask:

1. Can a reader answer one question in under 5 seconds?
2. Can I delete a sentence without losing information?
3. Could a table replace this paragraph?

Any "yes" → revise.

## Good vs bad

```markdown
✅
## Tiers

| Tier | Default | Blast-radius contained |
|------|---------|------------------------|
| 0 | yes (stock Docker) | host, Postgres, Redis via network isolation |
| 1 | gVisor host only | + syscall filtering + egress allowlist |

❌
> Status: **Tier 0 is the default (stock Docker); Tier 1 (gVisor + enforced egress
> allowlist) auto-applies only on a host with runsc registered; Tier 2 is an ops
> runbook below.** How to run a deployed JARVIS so a compromise — or a misbehaving
> agent — has a bounded blast radius, and exactly what each tier does and does not contain.
```

```markdown
✅
- **Follow-up window**: after a reply, the mic reopens ~7s with no wake word.
- **Trigger**: the client's `reply-ended` frame, sent when playback drains.
- ⚠️ **Caveat**: on headphones a window timed off the engine edge expires mid-reply.

❌
- **Follow-up (continued conversation)** — after a reply, `WakeListenSession` re-opens
  the mic for ~7s with no wake word and no acknowledgment, so a reply can be answered
  without saying "JARVIS" again. The trigger is the client's `reply-ended` control frame,
  sent when it finishes playing the reply (the final TTS chunk drains from its playback
  queue) — not the engine-side Speaking→Idle edge, which fires when the last chunk is sent…
```

## Related

- [README.md](README.md)
- [structure-and-navigation.md](structure-and-navigation.md)
- [diagrams.md](diagrams.md)
- [audit-and-rewrite.md](audit-and-rewrite.md)
