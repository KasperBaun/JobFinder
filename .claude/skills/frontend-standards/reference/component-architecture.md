# Component Architecture

How to structure a React + Next.js (App Router) frontend so that responsibilities are layered, pages stay thin, logic lives in hooks, and files are organized predictably. These are principles — apply them with judgment, but treat the layering rules and thresholds as defaults you need a reason to break.

## Strict Component Layering

Every UI element belongs to exactly one layer. Imports flow in one direction only: a layer may import from layers above it (closer to primitives) but never from layers below it (closer to pages). Never skip building a missing layer — if a composite is needed, create it rather than reaching into a feature.

| Layer | Purpose | May import from |
|-------|---------|-----------------|
| 1. UI primitives | Design-system atoms: button, card, table, input, modal, badge | Design tokens only |
| 2. Shared composites | Reusable assemblies: page header, pagination, state-view, data-browser, metric card | Primitives + tokens |
| 3. Feature components | Feature-scoped components, tied to one feature's domain | Composites + primitives + tokens |
| 4. Page components | Full-page compositions, one per route | Everything above |

Directional rules:

- ❌ Primitives must NOT import from composites or features.
- ❌ Composites must NOT import from features.
- ❌ A feature must NOT import from ANOTHER feature. If two features need the same component, promote it to the shared composite layer.
- ✅ Only page components (Layer 4) compose across all layers.

When you catch yourself importing a feature component into a different feature, stop: that component has outgrown its feature and belongs in Layer 2.

## Check the Catalog First

Before building any new UI element, search the existing primitive and composite catalog. Most needs — buttons, cards, tables, empty/loading/error states, pagination, toolbars — already exist. Creating a parallel implementation fragments the design system and guarantees visual drift.

- ✅ Reuse the existing primitive; extend it with a prop if it's close.
- ❌ Hand-roll a second button/card/table because you didn't look.

## Thin Route-Entry Files

The framework's route entry file (App Router `page.tsx`) is a thin shell: imports plus a single return statement. It contains ZERO logic, hooks, state, or styling. The real page lives in a page component one layer down, which the entry file simply renders.

✅ Correct thin shell:

```tsx
// app/feature/page.tsx
import { FeaturePage } from "@/.../FeaturePage";

export default function Page() {
  return <FeaturePage />;
}
```

✅ With route params (still thin — just forwards them):

```tsx
// app/feature/[id]/page.tsx
import { FeatureDetailPage } from "@/.../FeatureDetailPage";

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <FeatureDetailPage id={id} />;
}
```

❌ Wrong: data fetching, `useState`, conditionals, or inline JSX trees in the entry file.

## Page-Component Size Limits

The page component (Layer 4) is where complexity accumulates. When it crosses a threshold, extract — don't let it grow.

| Metric | Threshold | Action |
|--------|-----------|--------|
| Total lines | > 300 | Extract sub-components into a `components/` subfolder |
| `useState` calls | > 5 | Extract a `usePageName` hook |
| Logic before the `return` | > 50 lines | Extract a `usePageName` hook |
| Inline sub-component | > 50 lines | Move to its own file |
| Inline style block | > ~30 lines | Move styles to a co-located styles file |

These are tripwires, not hard caps — but crossing one is a signal to refactor now, not later.

## Hook Extraction Pattern

Co-locate a `usePageName` hook next to the page component. The hook owns all state, data fetching, mutations, and event handlers, and returns exactly what the page needs. The page component then only composes JSX — it reads values and renders.

```tsx
// usePageName.ts
export function usePageName(id: string) {
  const { data, isLoading, error } = useQuery(/* ... */);
  const [filter, setFilter] = useState("");
  const save = useMutation(/* ... */);

  const handleSave = () => save.mutate(/* ... */);

  return { data, isLoading, error, filter, setFilter, handleSave };
}
```

```tsx
// PageName.tsx
export function PageName({ id }: { id: string }) {
  const { data, isLoading, error, filter, setFilter, handleSave } = usePageName(id);

  if (isLoading || error) return <StateLikeFallback /* loading/error */ />;

  return (/* compose composites + primitives only */);
}
```

The page component should read top-to-bottom as a composition, with no business logic in sight.

## File Organization

Co-locate everything a page owns. Use named exports everywhere except framework route-entry files, which must default-export. Every directory that has multiple exports gets an `index` barrel.

Co-located page folder:

```
PageName/
├── PageName.tsx          # page component (named export)
├── usePageName.ts        # state, data, mutations, handlers
├── PageName.styles.*     # co-located styles (your styling solution)
├── index.ts              # barrel: export { PageName }
└── components/
    └── SubComponent.tsx  # extracted sub-component (> 50 lines)
```

Feature folder:

```
feature/
├── index.ts              # public barrel for the feature
├── components/           # feature-scoped components (Layer 3)
├── pages/
│   └── page-name/        # one co-located page folder per route
│       ├── PageName.tsx
│       ├── usePageName.ts
│       └── ...
├── common/               # feature-scoped shared helpers/components
├── hooks/                # feature-scoped hooks
├── lib/                  # feature-scoped utilities, types, mappers
└── tests/                # feature tests
```

Rules:

- ✅ Named exports for components, hooks, and utilities; barrel each multi-export directory.
- ✅ Default export ONLY in route-entry files (the framework requires it).
- ✅ Keep cross-feature shared code in the shared layers, never in a sibling feature's `common/`.
- ❌ No deep relative import chains across features — import through barrels and shared layers.
