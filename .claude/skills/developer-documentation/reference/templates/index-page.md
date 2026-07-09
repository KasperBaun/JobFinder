# Index Page Template

Every folder's `index.md` — the landing page a reader lands on and routes from. It routes; it does
not explain. Keep it ≤ 60 lines.

## Section order

| # | Section | Required | Notes |
|---|---------|----------|-------|
| 1 | `# <Folder Title>` | Yes | One H1, sentence-case |
| 2 | One-sentence purpose | Yes | What this folder documents — no "This document is…" |
| 3 | `## 📁 <Section>` + link table | Yes | One section per logical group; 2-column table |
| 4 | `## 📁 <Section>` (more) | Optional | Only if the folder has distinct groups |

## Skeleton

```markdown
# Architecture

How the system is built — layers, modules, and the decisions behind them.

## 📁 Design

| Doc | Covers |
|-----|--------|
| [Overview](./overview.md) | The 5-layer request flow and each layer's responsibility |
| [Modules](./modules.md) | The 6 bounded contexts and their boundaries |

## 📁 Decisions

| Doc | Covers |
|-----|--------|
| [ADR log](./adr/index.md) | 9 architecture decisions, newest first |
```

## Rules

| MUST | MUST NOT |
|------|----------|
| Lead the table's second column with a **summary** | Repeat the link title ("Overview doc") |
| Quantify when you can ("9 decisions", "all 18 hooks") | Leave the count vague |
| Use relative links (`./`, `child/index.md`) | Use absolute disk paths |
| Stay ≤ 60 lines | Explain concepts here — that belongs in the leaf doc |

> Delete this guidance block and the example rows when you copy the skeleton.

## Related

- [../structure-and-navigation.md](../structure-and-navigation.md)
- [../house-style.md](../house-style.md)
