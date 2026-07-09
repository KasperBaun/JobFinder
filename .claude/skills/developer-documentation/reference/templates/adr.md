# ADR Template

An **Architecture Decision Record** captures one decision that carries impact — a topology choice,
a framework, a storage engine, a boundary. One decision per file (MADR-lite), logged in `adr/index.md`.

## When to write one

| Write an ADR when… | Skip it when… |
|--------------------|---------------|
| The choice is hard to reverse (storage, topology, framework) | It's a local, easily-changed detail |
| Future readers will ask "why is it this way?" | The reason is obvious from the code |
| You rejected a credible alternative | There was no real alternative |

A typical project has **5–15** ADRs — enough to cover the load-bearing decisions, not every choice.

## File & log layout

```text
architecture/adr/
├── index.md                      # the log — one row per ADR, newest first
├── 0001-modular-monolith.md
├── 0002-postgres-over-mongo.md
└── 0003-react-vite-shell.md
```

| Rule | Detail |
|------|--------|
| Filename | `NNNN-kebab-title.md`, zero-padded, sequential |
| Numbers are stable | Never renumber or reuse — superseded ADRs keep their number |
| Superseding | New ADR; mark the old one `Superseded by [0007]`, don't delete it |

## ADR skeleton

```markdown
# 0002. Postgres over MongoDB

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-25 |
| **Supersedes** | — |

## Context

<The forces at play in 2–4 sentences: what problem, what constraints, what's at stake.>

## Decision

<One declarative sentence: "We will use Postgres as the primary store.">

<A few bullets of the reasoning if needed — **Term**: reason.>

## Consequences

- ✅ <good outcome — relational integrity, mature tooling>
- ✅ <good outcome>
- ❌ <cost accepted — no native document flexibility>

## Alternatives considered

| Option | Rejected because |
|--------|------------------|
| MongoDB | Relational core; joins dominate the access pattern |
| SQLite | Single-writer; prod needs concurrent writes |
```

## index.md log row

```markdown
| ADR | Decision | Status |
|-----|----------|--------|
| [0002](./0002-postgres-over-mongo.md) | Postgres as primary store | Accepted |
| [0001](./0001-modular-monolith.md) | Modular monolith topology | Accepted |
```

## Rules

| MUST | MUST NOT |
|------|----------|
| One decision per file | Bundle several decisions in one ADR |
| Status table at the top (Status/Date/Supersedes) | A multi-paragraph status preamble |
| Consequences split into ✅ gains / ❌ costs | List only the upsides |
| Name the rejected alternatives and why | Present the decision as the only option |
| Mark superseded ADRs, keep them | Delete or rewrite a decided ADR |

> The `Status / Date / Supersedes` table is the **one** allowed metadata table — a decision genuinely
> has a status, and git history alone doesn't surface "superseded". Delete this guidance when you copy.

## Related

- [../structure-and-navigation.md](../structure-and-navigation.md)
- [backend-architecture.md](backend-architecture.md)
- [index-page.md](index-page.md)
