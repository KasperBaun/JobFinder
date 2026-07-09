# Iconography

Emojis and symbols are **navigation aids**, not decoration. A fixed vocabulary, one meaning each,
makes a doc set scannable; freeform emoji makes it noise. Use only the set below.

## The vocabulary

| Symbol | Means | Use on |
|--------|-------|--------|
| 📁 | Section | `## 📁 Section` headings in `index.md` landing pages |
| 🧭 | Navigation / index | Links to a landing page or a "start here" pointer |
| ✅ | Do / correct / good | The good half of a Good-vs-Bad pair; `// ✅ GOOD` in code |
| ❌ | Don't / wrong / bad | The bad half of a Good-vs-Bad pair; `// ❌ BAD` in code |
| ⚠️ | Caveat / gotcha | A one-line blockquote warning of a sharp edge |
| 🔒 | Security | Auth, isolation, secrets, trust-boundary notes |
| 🚀 | Deploy / release | Deployment, pipelines, environments, runbooks |
| 🧪 | Testing | Test strategy, QA steps, coverage notes |
| 🔌 | Integration / API | External APIs, contracts, webhooks, message buses |
| 📦 | Module / package | A backend module, package, or bounded context |
| 🎯 | Purpose / goal | A doc's or section's objective, used sparingly |

## Rules

| MUST | MUST NOT |
|------|----------|
| Use a symbol for the one meaning above | Reuse 🚀 for "exciting" or ✅ for a bullet point |
| Keep ✅/❌ paired in comparisons | Sprinkle ✅ as decoration |
| Lead an `index.md` section with 📁 | Put 📁 on every heading in a leaf doc |
| Reach for a symbol when it aids scanning | Open a paragraph with three emojis |

## Domain icons (optional)

A doc MAY define a small, consistent set of **domain icons** beyond the core vocabulary — one glyph per named concept — where they aid scanning. Declare them in a legend (a parts table, or the first place they appear), then use them consistently.

| Rule | Detail |
|------|--------|
| One glyph, one concept | 🎙️ = ears/hearing everywhere; never reused for anything else |
| Declare on first use | A parts/legend table is the declaration |
| Keep the set small | A handful for a system's named parts — not a glyph per noun |
| Supplement, never replace | The core vocabulary still carries its own meanings |

Example — a system's four named parts: 🎙️ ears · 🧠 brain · 🔊 voice · ⚙️ engine.

## Logos & badges

| Use | How |
|-----|-----|
| Tech logos (a stack table) | A small inline image beside the name, editable source kept |
| Status/shield badges | Only on a repo `README.md`, never inside reference docs |
| Branded diagrams | See [diagrams.md](diagrams.md) — image + editable source |

## Good vs bad

```markdown
✅
## 📁 Architecture
| Doc | Covers |
|-----|--------|
| [Overview](./overview.md) | 5-layer request flow |

> ⚠️ **Caveat:** the engine network has no route to Postgres.

❌
## 🚀✨ Architecture 🎉
> 😀 This section is really important!! 🔥🔥
```

## Related

- [README.md](README.md)
- [structure-and-navigation.md](structure-and-navigation.md)
- [house-style.md](house-style.md)
