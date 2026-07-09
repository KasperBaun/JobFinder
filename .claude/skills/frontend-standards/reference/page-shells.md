# Page Shells & Composing From the Design System

Every page is assembled from a small, fixed set of canonical **shells**, and every shell is composed from shared design-system primitives. You never handwrite chrome — toolbars, dividers, headers, content frames, tables, badges. If a shell doesn't fit, you extend a shared primitive; you never invent bespoke chrome inside a feature.

## Core Principle

Before writing any chrome, **pick a shell**. A shell is a known recipe for arranging primitives. The decision is *which recipe*, not *how to build the frame*.

- ✅ Choose a canonical shell, then fill its slots with shared primitives.
- ✅ If no shell fits, **extend a shared primitive** (add a variant) or add a new shared composite.
- ❌ Never assemble a one-off frame (custom wrapper + divider + header) locally inside a feature.
- ❌ Never reach for raw HTML when a primitive exists for the need.

Chrome built locally is invisible debt: it drifts from the system, breaks theming, and can't be upgraded centrally.

## The Canonical Shells

### Decision tree

```
What is the page about?
├─ One resource, shown read-only as a document/file → File viewer shell
├─ One resource, full detail/edit                   → Detail shell
├─ Many resources (filter / sort / paginate)        → List shell
├─ At-a-glance metrics & summaries                   → Dashboard shell
└─ Multiple open documents, tabbed                   → Workspace shell
```

### Shell composition table

| Shell         | Outer wrapper            | Header                          | Body                                              | Footer        |
|---------------|--------------------------|---------------------------------|---------------------------------------------------|---------------|
| **Detail**    | content-container        | page-header                     | card sections                                     | —             |
| **List**      | content-container        | page-header                     | toolbar (search/filter/sort) + card+table or card-list | pagination |
| **Dashboard** | content-container        | page-header                     | grid of cards / metric cards                      | —             |
| **Workspace** | app-layout shell         | tabs (no page-header per panel) | per-tab inner shell                               | —             |
| **File viewer** | content-container       | sticky page-header w/ actions   | read-only document body                           | —             |

Notes:
- **Detail** — one resource: content-container → page-header → card sections.
- **List** — many resources: content-container → page-header → filter/search toolbar → card+table (or card-list via a view toggle) → pagination. Drive search/filter/sort/pagination state with a shared **data-browser / list hook**, not ad-hoc local state.
- **Dashboard** — content-container → page-header → grid of cards / metric (stat) cards.
- **Workspace** — app-layout shell + tabs. Each tab panel uses its **own** inner shell and does **not** repeat the page header.
- **File viewer** — the detail pattern with a **sticky** header carrying actions.

Schematic (placeholder names, not real component imports):

```tsx
// List shell, schematic
<ContentContainer>
  <PageHeader title="…" actions={…} />
  <Toolbar>{/* search + filters + sort + view toggle */}</Toolbar>
  <ListBody />            {/* conditional content — see below */}
  <Pagination />
</ContentContainer>
```

## List-Page Conditional Content Order

Inside a List shell's body, resolve state in this fixed order:

1. **Loading** → render your **state-view** primitive (loading variant).
2. **Error** → render your **state-view** primitive (error variant).
3. **Empty** → render your **empty-state** (the state-view's empty variant, or a dedicated empty-state primitive — same family).
4. **Data** → render your **data-table** or **card-list**.

Then render your **pagination** primitive *outside* the conditional, so it stays consistent across states.

```
loading? → state-view(loading)
error?   → state-view(error)
empty?   → empty-state
else     → table / card-list
———
pagination (always, below)
```

## Use the Right Primitive — Never Raw HTML

| Need                          | Use your … primitive            | Never hand-build                                   |
|-------------------------------|---------------------------------|----------------------------------------------------|
| Tabular data                  | data-table primitive            | raw `<table>` / `<tr>` / `<td>`                     |
| Status badge / tag            | chip / badge primitive          | `<span>` styled as a badge                          |
| Tab navigation                | tabs primitive                  | rows of `<button>`                                  |
| Card / bordered container     | card / container primitive      | `<div>` with border / shadow / radius              |
| User avatar                   | avatar primitive                | `<img>` + manual rounding/fallback                 |
| Single statistic / metric     | stat / metric card primitive    | bespoke number + label `<div>`                      |
| Page-through results          | pagination primitive            | raw prev/next links or buttons                      |
| Loading / empty / error state | state-view / empty-state primitive | centered `<div>` + spinner                       |
| Destructive confirmation      | confirm-dialog primitive        | ad-hoc modal / `window.confirm`                     |
| Page title + actions          | page-header primitive           | raw `<h1>` or styled header `<div>`                 |
| Page width / gutters          | content-container primitive     | arbitrary `max-width` `<div>`                       |
| App brand mark                | shared logo primitive           | recreated/inline SVG or `<img>` logo                |

## Banned Anti-Patterns

1. **Raw HTML tables** — bypasses sorting/density/theming. → Use/extend your **data-table** primitive.
2. **Inline `<span>` badges** — drifts from status color tokens. → Use your **chip/badge** primitive.
3. **Custom button tab bars** — loses keyboard nav & active styling. → Use your **tabs** primitive.
4. **Hand-built cards** (`<div>` + border/shadow/radius) — duplicates surface styling. → Use your **card** primitive.
5. **Recreated logos** — brand drift, no theme support. → Use the **shared logo** primitive.
6. **Inline / from-scratch state views** — inconsistent loading/empty/error UX. → Use your **state-view / empty-state** primitive.
7. **Duplicated pagination** — divergent controls across lists. → Use your **pagination** primitive.
8. **Bespoke page chrome** (custom toolbar + divider + wrapper) — reinvents a shell. → Compose a canonical shell from primitives.

## Closing Rule — Raise, Don't Localize

If a shell or primitive genuinely doesn't exist for your case:

- ✅ Add a **variant** to the relevant shared primitive, **or** add a new **shared composite** that any feature can reuse.
- ❌ Never solve it locally inside a single feature module.

A gap in the system is a signal to **extend the system**, not to fork it.
