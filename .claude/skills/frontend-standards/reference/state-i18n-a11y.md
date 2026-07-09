# State Views, Error Boundaries, i18n & Accessibility

Cross-cutting frontend standards for non-content states, route-level errors, internationalization, and accessibility/debug identity. Written for React + Next.js (App Router), but the principles are framework-agnostic. Snippets use placeholder names — map them to your own primitives.

## State views — one primitive for every non-content state

Every non-content state renders through **one shared state-view primitive** with variants. Never handwrite spinners, centered `<div>`s, or ad-hoc error blocks.

Variants:

- **loading** — work in progress
- **empty** — query succeeded, no rows
- **error** — query failed (wires a retry/refetch action)
- **forbidden / access-denied** — authenticated but not authorized

```tsx
<StateView variant="loading" />
<StateView variant="empty" title={t('noResults')} />
<StateView variant="error" onRetry={refetch} />
<StateView variant="forbidden" />
```

✅ One primitive, parameterized by variant.
❌ A bespoke `<Spinner />` in one screen and a centered `<div>Loading…</div>` in another.

## The conditional content pattern (order matters)

Data screens resolve state in a fixed order — first match wins, content is the fallback:

1. **loading** → `state-view(loading)`
2. **error** → `state-view(error)` wired to `refetch`/retry
3. **empty** → `state-view(empty)`
4. **data** → render the content (table / cards / detail)

```tsx
function FeatureList() {
  const { data, isLoading, isError, refetch } = useFeatureQuery();

  if (isLoading) return <StateView variant="loading" />;
  if (isError)   return <StateView variant="error" onRetry={refetch} />;
  if (!data?.length) return <StateView variant="empty" />;

  return <FeatureTable rows={data} />;
}
```

The error variant must always carry a path back to a working state — wire `onRetry` to the query's refetch.

## Route-level error handling

The framework's **route error file** (App Router `error.tsx`) is a **client component**. It renders the state-view **error** variant and wires the framework-provided reset/retry callback to the primitive's action:

```tsx
'use client';

export default function RouteError({
  error,
  reset,
}: {
  error: Error;
  reset: () => void;
}) {
  return <StateView variant="error" onRetry={reset} />;
}
```

The **layout-level error file** wraps subtree rendering with an **error-boundary primitive** so a render-time throw degrades to the error view instead of a blank page. Keep both framework-described: a route error file maps a thrown render to the error variant; an error boundary catches throws below it. Don't reach for a library-specific boundary when the framework already provides the seam.

## Internationalization (mandatory)

- **All** user-facing text goes through your i18n/translation hook (e.g. next-intl). Never hardcode JSX string literals.
- Scope keys to a namespace per feature/area: `const t = useTranslations('feature.area')`, then `t('title')`.
- **Every key exists in every supported locale.** Translations land together in the same change — never merge a feature with one locale populated and the rest missing or stale.

```tsx
// ❌ hardcoded literal
<h1>Customers</h1>

// ✅ namespaced, translated
const t = useTranslations('customers.list');
<h1>{t('title')}</h1>
```

Red flags:

- *"I'll add the other locale later"* — no. All locales land together, in the same change.
- A new key in one locale file and not the others — that's an incomplete change, not a follow-up.
- A literal string rendered in JSX — if a human reads it, it goes through the hook.

## Accessibility & debug identity

**Stable debug identifier.** Every exported component carries a stable identifier on its root element, derived from a unique-id hook. This aids debugging and test targeting:

```tsx
import { useId } from 'react';

export function FeatureCard() {
  const id = `FeatureCard-${useId()}`;
  return <article id={id}>{/* … */}</article>;
}
```

The prefix is the component name; the hook supplies the unique, stable suffix.

**Semantic HTML & a11y basics:**

- Real elements for real roles — headings for headings, `<ul>/<li>` for lists, `<button>` for actions (never a clickable `<div>`), `<label>` bound to every input.
- Respect focus management, add `aria-*` only where semantics fall short, and keep everything keyboard-operable.

✅ `<button onClick={…}>` with a visible label and reachable by Tab.
❌ `<div onClick={…}>` with no role, no focus, no keyboard handler.
