---
name: frontend-standards
description: Use when creating or modifying any React/Next.js frontend file (.tsx/.ts) — pages, components, styles, hooks, or i18n strings — and when verifying that frontend work is complete before committing, opening a PR, or marking a task done.
---

# Frontend Standards

## Overview

Four principles govern every frontend change:

1. **Compose from the design system — never handwrite chrome.** Pages are built from a small fixed set of shells out of shared primitives.
2. **Design tokens are the only source of design values.** No hardcoded colors, spacing, type, radii, or shadows.
3. **Pages are thin shells; logic lives in hooks.** Strict component layering, no fat page files.
4. **Every state and every string is handled, in every locale.** One state-view primitive; all user-facing text translated.

> **Role-based names.** This skill names components and tokens by *role* ("your page-header primitive", "your state-view primitive", "your design tokens") — map them to your project's design system. It assumes a React/Next.js frontend with a tokenized styling solution (StyleX, vanilla-extract, CSS variables, Tailwind tokens, …). The full detail lives in [`reference/`](reference/README.md).

## When to use

- **Before writing** any page, component, style, hook, or i18n string.
- **Before claiming done** — committing, opening a PR, or marking a task complete after touching frontend files.

## Page-shell quick guide

Pick a shell before writing any chrome. If none fits, **extend a shared primitive** — don't invent local chrome.

| Page kind | Composition |
|-----------|-------------|
| Detail (one resource) | content container + page header + card sections |
| List (many, filtered/paginated) | content container + page header + filter toolbar + table/card-list + pagination (drive state with a shared list/data-browser hook) |
| Dashboard | content container + page header + grid of cards / metric cards |
| Workspace (tabbed) | app-layout shell + tabs (tab panels don't repeat the page header) |
| File viewer | detail pattern with a sticky action header |

Full detail: [`reference/page-shells.md`](reference/page-shells.md).

## Never handwrite — use the primitive

| Need | Use your primitive | Never |
|------|--------------------|-------|
| Data table | table primitive | raw `<table>`/`<tr>`/`<td>` |
| Status badge/tag | chip/badge primitive | `<span>` styled as a badge |
| Tab navigation | tabs primitive | `<button>` rows |
| Card/container | card primitive | `<div>` with border/shadow/radius |
| Pagination | pagination primitive | hand-built prev/next |
| Loading / empty / error | state-view primitive | centered divs + spinners |
| Page header | page-header primitive | raw `<h1>` / styled header div |
| Content wrapper | content-container primitive | arbitrary max-width div |

## Tokens, not hardcoded values

Colors, spacing, typography, radii, borders, opacity, elevation, and motion all come from **semantic** design tokens. Never hardcode hex, `rgba()`, px, rem, or ms in a component. If a value isn't in a semantic group, **extend the group** — don't inline it. No inline `style={{…}}` unless the value is genuinely dynamic. Full model (primitive vs semantic, theming via token override): [`reference/design-tokens.md`](reference/design-tokens.md).

## Thin pages

| Metric | Threshold | Action |
|--------|-----------|--------|
| Route entry file (e.g. `page.tsx`) | any logic/hooks/styling | thin shell — imports + return only |
| Page component total lines | > 300 | extract sub-components |
| `useState` in a page component | > 5 | extract a `usePageName` hook |
| Logic before return | > 50 lines | extract a `usePageName` hook |
| Inline sub-component | > 50 lines | extract to its own file |

Layering, hook extraction, and file organization: [`reference/component-architecture.md`](reference/component-architecture.md).

## Read this before writing X

| Work type | Read first |
|-----------|-----------|
| New page / page shell | [`reference/page-shells.md`](reference/page-shells.md) |
| Any styling / color / spacing | [`reference/design-tokens.md`](reference/design-tokens.md) |
| New component / hook / file layout | [`reference/component-architecture.md`](reference/component-architecture.md) |
| Loading/error states, i18n strings, a11y | [`reference/state-i18n-a11y.md`](reference/state-i18n-a11y.md) |

Architecture index: [`reference/README.md`](reference/README.md).

## Completion checklist

Run against the frontend files you changed before claiming done. When a check flags a file, open it — don't skip.

| # | Check |
|---|-------|
| FE-PAGE-01 | Loading/empty/error states go through the shared **state-view** primitive — no bespoke spinners or centered divs. |
| FE-PAGE-02 | Route-level error file renders the state-view error variant with the framework's reset wired to its retry action. |
| FE-PAGE-03 | The route entry file is a **thin shell** — no hooks, state, or styling; imports + return only. |
| FE-CHROME-01 | No banned anti-patterns: raw HTML tables, `<span>` badges, `<button>` tab bars, `<div>` cards, hand-built pagination, bespoke page chrome. |
| FE-STYLE-01 | No hardcoded design values (hex, `rgba()`, px) outside the token/theme layer. |
| FE-STYLE-02 | No inline `style={{…}}` unless the value is genuinely dynamic. |
| FE-ARCH-01 | Component layering respected (primitives → composites → features → pages); no upward or cross-feature imports. |
| FE-I18N-01 | All user-facing strings use the translation hook, and every key exists in **every supported locale**. |
| FE-COMP-01 | Exported components carry a stable debug id on their root (e.g. derived from `useId()`). |

## Red flags — these are not exceptions

| Excuse | Reality |
|--------|---------|
| "It's just one hex code" | Tokens are the only source of design values. Use a semantic token or add one. |
| "I'll add the other locale later" | All locales land together. Add the translation now. |
| "A custom `<div>` is shorter than the card primitive" | Bespoke chrome is a banned anti-pattern. Use or extend the primitive. |
| "The page file only has one `useState`" | Route entry files are thin shells. Move state into the page component or a hook. |
| "The hardcoded color is only in dev" | The check still fails. Tokens, always. |
| "This screen is special, it needs its own chrome" | If no shell fits, extend a shared primitive — never invent local chrome. |

**Violating the letter of these rules is violating the spirit of them.** If a check is genuinely ambiguous, read the relevant [`reference/`](reference/README.md) file and follow it.
