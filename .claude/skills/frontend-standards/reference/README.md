# Frontend Development Rules

The full rule set for a React/Next.js frontend, abstracted to design-system-agnostic
principles. Component and token names are **role-based** — map them to your project's
design system.

## The four principles

```
1. Compose from the design system   → never handwrite chrome
2. Tokens are the only design values → never hardcode hex/px
3. Pages are thin shells             → logic lives in hooks
4. Every state & string is handled   → one state-view, every locale
```

## Rule files

- [page-shells.md](page-shells.md) — Canonical page shells (detail / list / dashboard / workspace / file viewer), the "use the primitive, never raw HTML" mapping, and the banned anti-patterns.
- [design-tokens.md](design-tokens.md) — Tokens by dimension, the primitive→semantic two-layer model, theming by token override, and the no-hardcode rule.
- [component-architecture.md](component-architecture.md) — The four-layer component hierarchy, thin route-entry files, page size limits, the hook-extraction pattern, and file organization.
- [state-i18n-a11y.md](state-i18n-a11y.md) — The single state-view primitive, route-level error boundaries, internationalization (every locale), and accessibility/debug identity.

## Quick decision

```
Building a page?        → pick a shell (page-shells.md); never invent chrome
Styling something?      → semantic tokens only (design-tokens.md); never hardcode
Page getting big?       → extract a hook / sub-components (component-architecture.md)
Loading/error/empty?    → the state-view primitive (state-i18n-a11y.md)
User-facing text?       → translation hook, every locale (state-i18n-a11y.md)
```
