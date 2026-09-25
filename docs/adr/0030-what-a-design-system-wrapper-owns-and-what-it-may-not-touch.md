# A design-system Wrapper adapts inwards. It owns the palette and its outer chrome, and never the geometry, the DOM or the state

A **Wrapper** (`ExGrid.MudBlazor`, and later Fluent / Bootstrap / in-house ones —
[ADR-0019](./0019-one-repository-many-packages.md)) is an adapter: it translates a design
system's vocabulary *into* the core's presentation contract
([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)–[0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md))
and adds its design system's outer chrome *around* the grid. The direction only ever points
inwards; nothing in the core knows any Wrapper exists, and the core takes no dependency
([ADR-0019](./0019-one-repository-many-packages.md) — enforced by project reference direction).

## The boundary

| The core owns | The Wrapper owns | The Wrapper must not |
|---|---|---|
| the resolved geometry and every number derived from it ([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)) | mapping its density words onto the core's presets | change any geometry from CSS — height, padding, margin, border on `ex-row`/`ex-cell`, or any `--ex-*` geometry token (ADR-0027 makes the attempt lose; layer 3 makes it fail a test) |
| the DOM structure and the internal classes ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)) | the values of the visual tokens, set on its own wrapping element | select internal classes, or touch `display` / `position` / `overflow` / `transform` of anything inside the root |
| Selection, Focus, editing state, keys ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)/[0012](./0012-anchor-focus-and-keyboard-navigation.md)) | its own outer controls (toolbar, pager shell, card); **and DOM focus on its own editor control** — the core owns the editor's box and keys, but a control the Chrome rendered is the Chrome's to focus when it appears and when the mode changes (F2), by Blazor's `FocusAsync`; the core has no reference to it and does not try | handle grid keys or the grid's Focus itself, or hold a second copy of any grid state |
| the class and state vocabulary, closed ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)/[0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)) | mapping its own states onto that vocabulary | invent per-wrapper state classes on grid elements |
| Chrome seams and their meaning ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) | Chrome implementations — filter panel, column menu, editor, loading — that render and call back | let Chrome decide meaning, or smuggle JavaScript in ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) names companion packages explicitly) |
| accessibility semantics — roles, names, indices, the announcement, the value behind `####` ([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md), [ADR-0016](./0016-column-width-and-overflow.md)) | making its palette keep the focus outline and selection visible | set or remove ARIA on grid elements, or style by ARIA attributes |
| the performance invariants P1–P9 (ADR-0027) | supplying `CellMetrics` — **all three widths** — for its own font (the metrics-bearing obligation, ADR-0027) | put a component in every cell (the measured 92ms path, [ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)/[0020](./0020-action-and-template-columns.md)), or add per-cell interop |

Two entries deserve their own sentence:

- **A Wrapper's editor control lives inside the box the core hands it** — `RowHeight` × the
  column width (ADR-0028). A `MudTextField` with its label, helper text and 48px of Material
  comfort does not fit a 22px cell and is the wrong control here; the Wrapper's editor Chrome is
  a bare input styled by tokens, and anything larger is a popover anchored to the cell.
- **A Wrapper's buttons do not replace the Action Column's.** The action button's box is
  *geometry* — an Auto Action Column's width is estimated from it
  ([ADR-0016](./0016-column-width-and-overflow.md)/[0020](./0020-action-and-template-columns.md)).
  A Wrapper restyles `ex-action`'s colours freely and its box not at all; a design-system icon
  button with its own dimensions belongs in a Template Column, declared and paid for.

## Verified against the first concrete case: MudBlazor

*(This section was rewritten on 2026-09-01, when the package started. The first version,
written before any Wrapper existed, sketched one component —
`<MudExGrid T="Trade" Color="Color.Primary" Dense="true" RowHeight="22" Elevation="2" … />` —
that forwarded every core parameter inward. Two things were wrong with it, and both are kept
here because they are the kind of wrong a future Wrapper will be tempted by again.)*

**What was wrong the first time.**

- **Forwarding.** The core has 39 parameters and passes its columns as one of them, so a
  forwarding component re-declares all 39 and must follow every one the core adds. That is
  not a boundary violation, but it is exactly the "growing beyond thin" this ADR warns about,
  and it guarantees a silent gap one day: a core feature unreachable through the Wrapper. The
  Wrapper's whole presentation job — compose one token string, map one density word, supply
  one `CellMetrics`, wrap one element — needs none of the 39.
- **`Color="Color.Primary"`.** A survey of MudBlazor v9.9 (`MudDataGrid`: 100 parameters,
  `MudTable`: 92) found that **neither table has a `Color` parameter**. Their selected row is
  coloured from the theme's palette by CSS; the only colour parameter is
  `LoadingProgressColor`. The sketch had invented an idiom. The Wrapper therefore exposes no
  `Color`: selection fill, Focus outline and the bands derive from `MudTheme`'s palette
  (Primary, and the dark/light variant in force) without a parameter.

**The shape that holds.** Two pieces, neither of which forwards anything:

```razor
<MudExGridPaper Dense="true" Hover="true" Elevation="2" Bordered="true">
    <ExGrid TRow="Trade" Source="_source" Columns="_columns" Chrome="MudGridChrome.Default" … />
</MudExGridPaper>
```

- **The outer element** (`MudExGridPaper` — the Wrapper's own component, named after
  MudBlazor's `MudPaper` because that is what it is: a surface with elevation, corners and an
  outline) carries the `mud-elevation-n` / square / outlined / bordered classes and hosts the
  `ToolBarContent` slot above the grid. The Visual Tokens are **not composed in C# at all**:
  the Wrapper's stylesheet maps each `--ex-*` token onto the variable `MudThemeProvider`
  already emits (`--ex-selection-fill: rgba(var(--mud-palette-primary-rgb), .18)`, and so
  on), scoped under `.mud-ex-grid`. A palette edit or a dark/light switch is then the
  browser recomputing variables — no string recomposed, no element touched, no render
  anywhere ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)). *(The
  first draft of this shape recomposed a token string per theme change; the stylesheet route
  is what a Consumer's CSS-only minimal Wrapper does, and there was no reason for the
  package to do more.)* A change of the paper's own parameters re-renders the paper and, as
  any parent's render does, the grid root once; the rows skip (ADR-0003).
- **A Chrome implementation** (`IGridChrome`) the Consumer passes to `ExGrid` as `Chrome`,
  rendering MudBlazor controls into the seams the core hands it.
- **Core parameters stay on `ExGrid`, in the core's words.** `ViewportHeight`, `RowHeight`,
  `PinnedColumnCount` and the rest are written by the Consumer on the core component. A
  Wrapper that translated MudBlazor's `Height="400px"` would be parsing a CSS string into
  geometry — a guess where ADR-0028 wants a number.

**The one thing the two-piece shape could not do, and the one core change it costs.** The
metrics-bearing obligation (ADR-0027): whoever sets `--ex-font-family` owes `CellMetrics`.
The outer element sets the font (Roboto) but cannot reach the inner `ExGrid`'s parameters, so
in the first draft of this shape the Consumer had to remember
`CellMetrics="MudExGrid.RobotoMetrics"` by hand — and forgetting it is the quiet failure this
component is built to refuse: Roboto on screen, `system-ui` widths in the `####` and Auto-width
arithmetic. The obligation must not be split across two components. So **the core reads one
cascaded value** — `GridPresentationDefaults`: the three glyph widths stated at the size they
were measured, scaled by the core to the resolved font size; a default `Density`; a
default for the hover band's switch; and, since
[ADR-0038](./0038-row-stripes-are-painted-from-the-rows-absolute-position.md), a default for Row
Stripes — and applies each **only where the corresponding
parameter is not set explicitly**; an explicit parameter still beats it, per value, as
ADR-0028 says of presets. The outer element cascades Roboto's widths together with the font
token, so the font and its metrics leave the same hand. The cascaded object is one of four
static instances (Dense × Hover), so it changes only when those two flags do — which is a
tidiness, not a render guarantee: a parent's render reaches the grid root whatever the
cascaded reference is, and it is the rows' own value comparison that skips them (ADR-0003).
The core learns nothing about MudBlazor: the cascaded type is the core's own, and a
Consumer's CSS-only minimal Wrapper may cascade one just the same. *(The
earlier sentence "nothing on this list required a core change beyond ADR-0028" is therefore
corrected: this one type is the exception, and it exists to keep the metrics obligation
whole. The hover switch rode along for the same reason: MudBlazor's `Hover` is a word on the
table, and the band's switch is the grid's parameter — ADR-0029 — so the only honest way for
the paper to say it is as a default the grid's own value beats.)*

**The MudBlazor surface, sorted.** Every `MudDataGrid` / `MudTable` parameter falls into one
of four rows, and the rule for each row is the reason a parameter lands there.

| Sorted as | Parameters | Because |
|---|---|---|
| **Outer element's own** | `Elevation` (1), `Square`, `Outlined`, `Bordered` → `--ex-column-rule-color` visible, `ToolBarContent`, `Class` / `Style` on the outer element only | the value is a token or lands outside the instance root; `ExGrid` itself has no `Class` / `Style` ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)) |
| **Mapped onto the core, through the cascade** | `Dense` → `Density.Compact`, false → `Standard`; `Hover` → `HighlightHoverRow`, the band coloured by the palette's table-hover ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md), [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) fifth entry); `Striped` → `StripeRows`, the stripe coloured by the palette's table-stripe colour ([ADR-0038](./0038-row-stripes-are-painted-from-the-rows-absolute-position.md) — moved here from "Not offered" on 2026-09-24) | Material's dense is not a spreadsheet row ([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)); the band's switch is the grid's parameter and its colour a token, and the Wrapper reaches the parameter the only way it can — as a default the grid's own value beats |
| **A seam's content** | the loading bar → `LoadingIndicator` (`MudProgressLinear`, indeterminate, where the core places the seam) with `LoadingProgressColor` (Info) **on the Chrome**, whose content it colours; the editor → `CellEditor`, a bare input in the core's box with Material's underline painted; `FilterTemplate` and the menu icons → `FilterPanel` / `ColumnMenu` / `ContextMenu`, built from MudBlazor controls inside the core's popover, Inner Popups (`MudSelect`, `MudDatePicker`) included ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)) | the seam exists and the core keeps the meaning ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) |
| **Not offered, and why** | *(`Striped` stood here until 2026-09-24 — "needs a row-parity class the core does not emit, and `nth-child` flips with the Window's first index (reserved, trigger: a Consumer asking for stripes)". The trigger fired, and the parity was already there: ADR-0038.)* `FixedHeader`, `Virtualize`, `ItemSize`, `OverscanCount`, `HorizontalScrollbar` — always so, nothing to map; `Breakpoint` — no stacked layout, because every core rule is defined on a grid of cells and the grid targets a desktop ([ADR-0043](./0043-exgrid-is-a-desktop-grid.md); *this cited ADR-0031 until 2026-09-25, which is about right-to-left layout and gave no reason*); `RowClass/Style(Func)`, `CellClass/Style`, `HeaderClass` — per-row and per-cell styling is closed, Row Kind and Cell State are the vocabulary ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)); `LoadingContent`, `NoRecordsContent`, `PagerContent` — the empty body is the Placeholder mechanism ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)) and the pager has no seam yet; `Height` — a CSS string the Wrapper would have to parse into geometry, where ADR-0028 wants a number on the grid; every behaviour parameter (`Items`/`ServerData`, sorting, filtering, grouping, selection, editing, paging) — the grid neither holds nor executes ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)/[0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)); a Wrapper accepting one would only forward it to the Consumer under a name that no longer means what it did | a MudBlazor word that would change meaning inside this contract is refused rather than accepted and quietly redefined |

Nothing in the table required the core to reference a MudBlazor type, and nothing but the
cascaded presentation default required a core change. That is the check this section exists
to record.

**One thing the palette could not supply.** The Focus outline was first mapped onto the
palette's primary, which is what Material draws its focus indicators in. The browser suite
measured it against the dark surface at **2.83:1**, under the 3:1 the Definition of Done's
UX-9 requires — the same shortfall, one palette over, that had already put `CanvasText` rather
than `Highlight` on the core's default. The Wrapper's Focus outline is therefore the palette's
ink colour (`text-primary`), which contrasts with its own surface by construction in both
schemes; the selection fill, the Focus band and the root's outline keep the primary. The
requirement was not relaxed to fit the palette.

## How a violation is caught rather than trusted away

The contract is enforceable because each prohibition lands somewhere observable:

- **Geometry from CSS** — the cascade defeats it short of `!important` (ADR-0027), and the
  browser layer asserts painted row height, header height and cell padding equal the declared
  metrics with the Wrapper's stylesheet loaded
  ([ADR-0026](./0026-layer-three-runs-on-playwright-against-the-installed-chrome.md)).
- **A dependency pointing the wrong way** — the build: the core has no reference to give it.
- **JavaScript in a Wrapper** — review against ADR-0021's list, which already names companion
  packages.
- **Per-cell components** — the render-count layer (ADR-0003's bUnit tests) run against the
  Wrapper's pages, the same way the core's are.
- **A second copy of state** — has no API to feed it: Selection and Focus arrive by event and
  cannot be pushed back in.

## Consequences

- **`Wrapper` and `Theme` become terms in `CONTEXT.md`.** A Theme is a set of visual token
  values; a Wrapper is a package that produces one from a design system and adds outer chrome and
  Chrome implementations. Chrome (behavioural seams) and Theme (appearance) stop sharing a
  boundary word.
- **A Wrapper is thin by design.** Its whole presentation job is: compose one token string, map
  two enums, supply one `CellMetrics`, wrap one element. If a Wrapper is growing beyond that plus
  Chrome implementations, it is holding something the boundary says it must not.
- **In-house design systems get the same deal with no package at all**: a Consumer with a CSS
  file that sets visual tokens on a container *is* a minimal Wrapper. The stable surface
  (ADR-0029) is the entire integration contract.

## Open

- ~~Wrapper stylesheet load order~~ — resolved: the order is deliberately **not** specified,
  and the contract is written so it cannot matter. Token values are order-free by nature
  (inheritance, not override). A Wrapper rule that does target a stable class **must be scoped
  under the Wrapper's own element class** (`.mud-ex-grid .ex-header-cell`, never a bare
  `.ex-header-cell`): two classes outrank one whatever order the stylesheets loaded in, so the
  same markup paints the same everywhere. Pinning `<link>` order instead was rejected — nothing
  in Blazor's static-asset story enforces it, and a contract nobody can enforce is a bug
  generator.
- ~~RTL~~ — resolved: LTR-only ([ADR-0031](./0031-the-grid-lays-out-left-to-right-only.md));
  a Wrapper on an RTL page leaves the grid an LTR island and does not try to flip it.
- ~~Which Chrome seams `ExGrid.MudBlazor` implements first~~ — the trigger fired (the package
  started 2026-09-01), and the first thing settled was **what the package is for**, because
  the seam order follows from it. No Consumer on MudBlazor exists yet (the first Consumer,
  `poke`, does not use it), so `ExGrid.MudBlazor` is built **as the proof of this boundary**:
  the first real Wrapper, whose job is to show that one design system can be wrapped inside
  this contract and to discharge the Definition of Done's "stub Wrapper" criteria (UX-3/6/9)
  with a real one. The seam order is therefore a verification order, not a product order —
  it begins where the boundary is most likely to break: **(1) no seam at all** — the outer
  element, the palette-derived tokens, the cascaded Roboto metrics, and the browser layer
  asserting painted geometry equals declared geometry with the real stylesheet loaded;
  **(2) the Cell Editor**, the one seam that must fit inside the box the core hands it and the
  one a Material control most wants to overflow; **(3) the loading bar**; **(4)** the filter
  panel, the column menu and the Context Menu, started 2026-09-24, as content inside the core's
  own popover. *(This item used to end "— never a `MudPopover`, whose provider renders outside
  the instance root and would break the three dismissals of ADR-0010 and the independence of
  ADR-0018". That was a prediction, made before any seam held one. Read against MudBlazor 9.9's
  source it was mostly wrong — Escape closes the inner popup first and then the popover, and
  under MudBlazor's default a press elsewhere still reaches the grid — and a seam that a design
  system's ordinary select cannot live in is not much of a seam.
  [ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md) replaces it:
  a seam's contents may hold **Inner Popups**, the grid's own elements still never leave its
  root, and layer 3 verifies the rest rather than trusting it.)* The package also serves as the
  place the Wrapper is verified against a Consumer: a proof-of-concept page in the DemoHost shaped
  like an ordinary MudBlazor application, with nothing of any real Consumer's domain in it.
