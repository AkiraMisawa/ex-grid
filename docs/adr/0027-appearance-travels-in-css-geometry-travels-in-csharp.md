# Appearance travels in CSS variables; geometry travels in C#. No colour is a parameter, and no pixel is only a stylesheet value

The grid is meant to carry design-system wrappers — `ExGrid.MudBlazor` first
([ADR-0019](./0019-one-repository-many-packages.md)) — without the core learning anything about
any design system. Everything a Wrapper or a Consumer wants to change falls into exactly **two
kinds**, and the whole presentation contract is that they travel by **different routes**.

| | **Geometry** | **Appearance** |
|---|---|---|
| What it is | Row Height, Header Height, cell padding, font size, digit width, the Viewport's size | every colour, weight, family, outline style, decoration, shadow, radius, opacity |
| Who decides | the Consumer or the Wrapper, through a **C# parameter** | the Wrapper, **in CSS** |
| Where the truth lives | one resolved `GridMetrics` ([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)) | the cascade |
| How it reaches the DOM | custom properties written **inline on the instance root**, from that one value | custom properties with defaults in `ex-grid.css`, overridden anywhere at or above the root |
| What a change costs | one render of the root; the virtualisation arithmetic recomputes with it | **nothing at all in .NET** |
| What using the other route costs | the arithmetic and the paint drift apart — *unsupported* ([ADR-0013](./0013-fixed-row-height.md)) | a colour dragged through .NET for no reason |

The rule in one line: **no colour is ever a C# parameter, and no pixel is ever only a stylesheet
value.**

## The third kind, which is where this gets got wrong

A few tokens *look* like appearance and are **metrics-bearing**: changing one changes how wide a
glyph is, and glyph width is an input to `####` and to Auto width
([ADR-0016](./0016-column-width-and-overflow.md)).

```
--ex-font-family     --ex-font-weight     letter-spacing     font-stretch     font-feature-settings
```

Whoever sets one of these **owes the core a new `CellMetrics`** — **three widths, not one**
*(revised once the default was measured; see the block below)*. `CellTextMetrics` carries a wide,
a digit and a narrow width, and each is contractually at least as wide as any glyph of its class
that the column's formats emit. Overshooting shows `####` one glyph early (a hover); undershooting
clips a number so that it **looks like a different valid number** — the failure ADR-0016 exists to
prevent. The obligation cannot be enforced by a type, so it is written down here and repeated in
[ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md) as a Wrapper's
duty.

> **The core's own stylesheet already trips this.** `.ex-row-group` and `.ex-row-total` paint at
> `font-weight: 600` ([ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md)), while
> every width estimate is made at one digit width. A bold tabular digit is wider than a regular
> one in most families, so a number that estimated as fitting can clip **on a total row only** —
> which is the row a reader is most likely to be reading a number off. ADR-0016's digit-width
> contract therefore reads *at least as wide as any glyph **any painted variant** emits*, and the
> core's own bold Row Kinds are the first thing it has to cover. Recorded here rather than fixed
> here: the fix is either a wider default digit width or a Row Kind that is not bold, and that is
> a measurement, not a reasoning ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)'s
> rule about performance applies to metrics too).
>
> **Measured, and it was the smaller of the two problems.** At `system-ui`/14px a tabular digit is
> 8.668px at weight 400 and **9.058px at 600**, so the bold Row Kinds do undershoot a declared 9 —
> by 0.69px over twelve digits. The measurement also found something this paragraph was not looking
> for: **`%` is 13.836px**, so the contract breaks by 54% on any percent column, at any weight.
> Neither of the two fixes named above would have caught that, and a single grid-wide number cannot
> serve a twelve-digit amount column and a percent column at once — so the resolution is a third
> option, **charging per character class**, recorded with the full table in
> [ADR-0016](./0016-column-width-and-overflow.md). This paragraph was right that it was a
> measurement and not a reasoning; it was wrong about what the measurement would say.

## How the split is enforced rather than merely asked for

**Geometry tokens are written inline on the instance root.** An inline declaration outranks every
stylesheet rule, every cascade layer and every inherited value, so a Wrapper that sets
`--ex-row-height` on an ancestor — or in its own stylesheet — **loses**, and the grid keeps
painting what its arithmetic believes. That is not politeness; it is the cascade.

**Visual tokens are only ever read, never written by C#.** `ex-grid.css` reads each one with a
default (`var(--ex-selection-fill, rgba(60, 120, 216, 0.18))`), and custom properties inherit, so
a Wrapper sets them on **its own element wrapping the grid** and the grid picks them up. This is
why the core needs **no theming API at all**: there is nothing for a Wrapper to call. It also
keeps ADR-0018's rule — variables belong to an instance, never to `:root` — true by construction,
because the Wrapper's element is per instance too.

What cannot be prevented is `!important`, or a rule that bypasses the token entirely:

```css
.ex-row { height: 24px !important; }   /* unsupported. the virtualiser still believes 28 */
```

Nothing in CSS can stop that. So it is **named unsupported**, and the invariant it breaks is
asserted where it can be observed — in the browser layer, against a real painted grid
([ADR-0026](./0026-layer-three-runs-on-playwright-against-the-installed-chrome.md)): the height a
row is painted at equals the `RowHeight` that was declared. A violation then fails a test rather
than showing up months later as a selection outline half a row off.

## Considered Options

- **A `GridTheme` object in C#, carrying colours** — rejected. Every palette change becomes a
  parameter change and a render, and the colours have to be serialised into inline styles or a
  generated `<style>` block, which allocates on the render path. Worse, it puts colour where it
  can be reached: the first convenience is "pass the row's colour into the row component", and
  that is exactly the parameter [ADR-0008](./0008-selection-is-painted-by-an-overlay.md) refused
  to add for selection. A dark/light switch would cost a full re-render of a grid that the
  browser could have repainted alone.
- **CSS custom properties for everything, geometry included** — rejected, and it is the option
  ADR-0013 already refused: CSS at 24px and C# at 28px puts the scroll position, the selection
  overlay and the editor slightly out of alignment, which is a defect nobody can see the cause
  of.
- **Class-based themes (`.ex-theme-dark`, `.ex-theme-mud`)** — rejected. It multiplies against the
  interned class tables in `CellClasses`/`RowClasses`, it cannot express a palette that is only
  known at runtime (a `MudTheme` the Consumer built in code), and switching it re-renders every
  cell to change a class that only ever meant a colour.
- **The hybrid above** — taken. The split is not a compromise between the two; it is the line
  between *what the arithmetic reads* and *what only the browser reads*.

## The invariants this contract exists to protect

Numbered so that a review, a Wrapper author or a future ADR can cite one.

| | Invariant | Where it comes from |
|---|---|---|
| **P1** | The number of DOM nodes is a function of the Viewport, never of the result | [ADR-0004](./0004-cap-the-cells-touched-per-frame.md) |
| **P2** | The number of component instances likewise; the memoisation boundary is the row, and only the row | [ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md), [ADR-0020](./0020-action-and-template-columns.md) |
| **P3** | An **appearance** change re-renders nothing in .NET. A **geometry** change renders the root and mounts or unmounts only the rows that entered or left the Viewport; it re-renders an existing row only when a value that row paints from has actually changed | new here |
| **P4** | No per-cell JS interop, and no layout read on the path to a paint | [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) |
| **P5** | Per-cell strings are interned or cached alongside the geometry that produced them, never composed inside the render loop | [ADR-0016](./0016-column-width-and-overflow.md) as implemented; formalised here |
| **P6** | The Row Height the virtualiser uses and the Row Height the browser paints are the same number, from one place | [ADR-0013](./0013-fixed-row-height.md) |
| **P7** | Selection, Focus and uncommitted editor text outlive the DOM elements they are painted over | [ADR-0008](./0008-selection-is-painted-by-an-overlay.md), [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) |
| **P8** | Nothing inside the row Viewport transitions or animates | new here — see below |
| **P9** | A Wrapper adds no geometry, no JavaScript, no per-cell component and no state of its own | ADR-0021, [ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md) |

**P5 governs the strings the grid composes** — classes, styles, ids, `aria-colindex`, the run of
`####`. *(Scoped 2026-09-24, when PF-3 was first measured: the measurement found the grid's own
per-cell strings composed per render — the cell id, `aria-colindex`, `####`, an action's class,
and under header groups each leaf's and each rectangle's style and `aria-colspan` — and fixed
them, and found one cost that is not the grid's.)* An event
directive — `@onmousedown:stopPropagation`, `:preventDefault` — has Blazor compose the
attribute's internal name on every render, some 100 bytes a directive, and an Action or
Template cell carries several, as does a header's menu button. That cost is the framework's,
and it stays. The only way round it is to write the internal name out pre-composed, and that
name is interpreted by whichever Blazor the **host** runs: this package is built on net8.0 and
runs on everything newer ([ADR-0022](./0022-packages-target-net8-and-run-on-everything-newer.md)),
so a rename in a later release would let a press on an action through to the Viewport — the
selection moving under it, with no error — and nothing run on net8.0 would notice. Being
quietly wrong is refused in favour of a small allocation.

**P8 is the one that will look like an arbitrary restriction.** Rows are recycled: the element
that painted row 400 paints row 460 after a scroll. A `transition` on a cell's background or
colour therefore animates *from the previous row's value to this one's* — a wave of colour
crossing the grid on every scroll, worst exactly where the data changes most. Transitions on the
root, the header or a popover are fine; inside `.ex-viewport` they are not.

## Consequences

- **A Wrapper needs no API from the core to theme the grid.** It renders its own element around
  `<ExGrid>` and sets `--ex-*` on it. Nothing is passed, nothing is compared, nothing re-renders.
- **The defaults stay on CSS system colours** (`Canvas`, `CanvasText`, `Highlight`). An untouched
  grid then follows the host's `color-scheme` into dark mode and follows a forced-colors setting,
  without the core owning a palette or a dark theme of its own. The core does **not** declare
  `color-scheme` itself — that belongs to the host page.
- **Forced colors needs its own block, and it is not optional.** Cell State is painted with
  background *images* and a `box-shadow` corner mark precisely because neither occupies layout
  ([ADR-0013](./0013-fixed-row-height.md)) — and those are among the mechanisms a forced-colors
  mode discards. Which ones exactly is a browser detail, so the design must not depend on it:
  `@media (forced-colors: active)` restates every state in system colours and borders that are
  painted, not laid out. Unwritten today; named as required work in
  [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md).
- **`GridAction.CssClass` is not an exception.** It is a class name for an icon, which is
  appearance; no colour crosses the boundary.
- **This ADR does not say what the tokens are called.** The vocabulary, and which classes a
  Wrapper may select at all, is ADR-0029. What geometry consists of, and how it is resolved, is
  ADR-0028.

## Open

- ~~RTL~~ — resolved: the grid is LTR-only, by decision rather than omission
  ([ADR-0031](./0031-the-grid-lays-out-left-to-right-only.md)).
- ~~Print~~ — resolved: **printing is not supported**, and no `@media print` block dresses it
  up. The grid holds a Window, not the result ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)),
  so browser print can only ever emit the painted slice — and ~20 rows typeset to look like a
  finished table is a partial print that reads as a whole one, which is the quiet wrongness this
  design refuses. The working exits already exist: copy carries raw values to Excel
  ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)), and a report is the Consumer's to
  produce from the data it owns.
- ~~Zebra striping~~ — resolved: **not offered**, and it cannot be added in CSS anyway
  (`:nth-child()` counts painted siblings, so under virtualisation the stripes crawl on every
  scroll; a correct stripe needs an absolute row index threaded through every row). The need it
  would have served — telling which row you are on — is served instead by the **Focus band**,
  one overlay rectangle specified in
  [ADR-0008](./0008-selection-is-painted-by-an-overlay.md).
