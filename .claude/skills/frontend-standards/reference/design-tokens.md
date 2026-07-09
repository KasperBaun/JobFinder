# Design Tokens Are the Only Source of Design Values

Every design value in a frontend — color, spacing, typography, radius, borders, opacity, elevation, motion — must come from a **design token**. Components never carry raw values. This makes theming, consistency, and global change a matter of editing tokens, not chasing literals across a codebase.

This document is tool-neutral. Apply it with whatever your styling solution is (StyleX, vanilla-extract, CSS variables/custom properties, Tailwind with a token config, etc.). The shape of the system matters far more than the API you express it in.

## Core Principle

No component may hardcode a hex color, `rgba(...)`, `px`, `rem`, or `ms` value. Every static visual value resolves through a token.

✅ Color, spacing, typography, radius, borders, opacity, elevation/shadow, and motion (duration + easing) all flow from tokens.
❌ `color: #1a73e8`, `padding: 16px`, `border-radius: 8px`, `transition: 200ms ease-out` written inline in a component.

If a value is genuinely dynamic (a computed width percentage, a runtime-measured offset), an inline style is acceptable — see [Inline Styles](#inline-styles). Everything static goes through tokens.

## Organize Tokens by Design Dimension

Create **one token group (or file) per design dimension**, not per tier:

| Group | Holds |
|-------|-------|
| colors | palette ramps + semantic surfaces, foregrounds, accents, status |
| typography | font families, sizes, weights, line-heights, letter-spacing |
| spacing | the spacing scale (margins, padding, gaps) |
| radius | corner radii |
| borders | border widths + styles |
| opacity | opacity steps |
| elevation | shadows / z-layering |
| motion | durations + easing curves |

Rationale: a developer asking "how does color work here?" finds the **entire** color story — primitives and semantics — in one place, instead of reassembling it from a "primitives" file and a separate "semantics" file.

## Two-Layer Model: Primitive vs Semantic

Within each dimension, tokens live in two layers.

**Primitive tokens** are the raw scales and ramps — the design system's vocabulary:
- A color palette of several ramps × ~12 steps each.
- A spacing scale on a consistent grid (e.g. a 4px base unit).
- A modular type scale.

Primitives are **role-named, not hue-named** (`gray`, `accent`, `danger` — not `blue700`).

**Semantic tokens** are **intent-named** — they describe *what a value is for*, and their defaults reference primitives:

| Category | Example semantic tokens |
|----------|--------------------------|
| Surfaces | `bgCanvas`, `bgSurface`, `bgSurfaceRaised` |
| Foregrounds | `fgDefault`, `fgMuted` |
| Accents | `bgPrimary`, `fgOnPrimary` |
| Status | `success`, `warning`, `danger`, `info` |
| Borders | `borderDefault`, `borderStrong` |
| Overlays | `overlayScrim` |

**The rule:** components consume **only semantic tokens**. Primitives exist for **theme authors**. A component never imports a primitive (rare exceptions: theme-stable absolutes like `transparent`, `white`, `black`).

### The resolution chain

A value resolves through a fixed chain — consumer → semantic → primitive → terminal value:

```
Button background
  → bgPrimary            (semantic: "the primary accent surface")
     → accent.step9      (primitive: the vivid solid step of the accent ramp)
        → #3b6cf0        (terminal value — lives ONLY in the primitive layer)
```

The component knows `bgPrimary`. It never knows `accent.step9`, and it certainly never knows `#3b6cf0`.

## Theming = Overriding Token Values at the Root

A theme is **not** a set of component variants. A theme is a set of **semantic-token overrides applied at a root scope**. Components are written once and never change between themes.

```
:root            { --bg-canvas: <light primitive> }
:root[data-theme="dark"] { --bg-canvas: <dark primitive> }
```

Swapping the theme rebinds the semantic tokens at the root. Because components read those tokens through the cascade (CSS custom properties or your solution's equivalent), the new values **propagate purely via the cascade — no component re-renders, no prop changes, no conditional logic**. Flip the attribute on the root; the whole tree repaints.

A typical topology is `light` / `dark` / `system` (the last following the OS preference) — but that's an example, not a requirement.

**Therefore:** never override a semantic token from inside a component. Overriding token values is exclusively the theme layer's job.

## Extend, Don't Inline

When you need a value that isn't in the relevant semantic group:

✅ **Extend the semantic group** — add a new intent-named token (and point its default at the right primitive).
❌ Hardcode the literal in the component.
❌ Reach past the semantic layer to grab a primitive directly.

Adding an entirely **new design dimension** follows the same shape: create a new token group with both a primitive layer (the raw scale) and a semantic layer (intent-named tokens referencing it).

## Inline Styles

Inline styles are permitted **only** when the value is genuinely dynamic and cannot be known at authoring time:

✅ `style={{ width: `${percent}%` }}` — a computed progress width.
❌ `style={{ padding: '16px' }}` — static; use a spacing token.
❌ `style={{ color: '#333' }}` — static; use `fgDefault`.

## Component Consumption Rules

✅ Import semantic tokens, grouped by dimension.
✅ Let themes — not components — decide what a semantic token resolves to.
❌ Never import a primitive token into a component (except theme-stable absolutes like `transparent`/`white`/`black`).
❌ Never hardcode a hex, `rgba`, `px`, `rem`, or `ms` value.
❌ Never override a semantic token from inside a component.

## Appendix: A Common Ramp Pattern (Radix-inspired)

One widely-used convention gives each color ramp **12 steps**, each step assigned a *role* rather than a brightness. This is an illustration of how a primitive ramp can be structured — a useful default, not a mandate:

| Step | Role |
|------|------|
| 1 | app background |
| 2 | subtle background |
| 3 | UI element background |
| 4 | hovered UI element background |
| 5 | active / selected UI element background |
| 6 | subtle borders & separators |
| 7 | UI element border |
| 8 | hovered UI element border |
| 9 | vivid solid (the ramp's strongest fill) |
| 10 | hovered vivid solid |
| 11 | low-contrast text |
| 12 | high-contrast text |

Naming steps by role (not by hue or brightness) is what lets a dark theme remap step 1 to a near-black and step 12 to a near-white while every semantic token keeps pointing at the *same step number* — and every component keeps working untouched.
