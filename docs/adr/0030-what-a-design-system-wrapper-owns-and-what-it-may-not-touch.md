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
| Selection, Focus, editing state, keys ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)/[0012](./0012-anchor-focus-and-keyboard-navigation.md)) | its own outer controls (toolbar, pager shell, card) | handle keys or focus itself, or hold a second copy of any grid state |
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

```razor
<MudExGrid T="Trade" Color="Color.Primary" Dense="true" RowHeight="22" Elevation="2" ... />
```

What the Wrapper renders — and everything it needed is already in the contract:

```razor
<div class="mud-ex-grid mud-elevation-2" style="@_tokens">   @* its element, its elevation *@
    <ExGrid TRow="Trade" Density="GridDensity.Compact" RowHeight="22"
            CellMetrics="RobotoMetrics" ... />
</div>
```

| Wrapper parameter | Becomes | Route |
|---|---|---|
| `Color="Color.Primary"` | `--ex-selection-fill: rgba(<palette.Primary>, .18); --ex-focus-outline: 2px solid <palette.Primary>; …` composed **once** into `_tokens` when the theme changes | visual tokens on the wrapping element; custom properties inherit, so the core needs no theming API and no render happens per cell (ADR-0027) |
| dark/light switch, palette edit | the same string recomposed; one element's `style` changes | zero renders inside the grid |
| `Dense="true"` | `GridDensity.Compact` | **not** `Excel` — Material's dense is not a spreadsheet row (ADR-0028); the Wrapper may expose the core's `Density` alongside for the full range |
| `RowHeight="22"` | the core's `RowHeight` | explicit beats the preset (ADR-0028); virtualiser, DOM, overlays and editor all follow the one value |
| `Elevation="2"` | `mud-elevation-2` on the wrapping element | never a shadow on the scroller: outside the root entirely, so it cannot be clipped by the scroll container or mistaken for a rule |
| MudTheme typography (Roboto) | `--ex-font-family` on the wrapping element **plus** `CellMetrics` measured for Roboto at the resolved size — a wide, a digit and a narrow width | the metrics-bearing obligation (ADR-0027). *(This row read "Roboto at 14px measures ≈8.4px, declared as 9 — over is the safe direction" while `CellMetrics` was one number. One number was not enough: measured against the system stack, `%` is 54% wider than a digit, so a single declared width either inflates the amount columns or clips the percentages — ADR-0016 has the table. Declaring one width per class is the same duty, done three times; overshooting each is still the safe direction.)* |
| its icons, its toolbar, its pager | its own markup outside the root; `GridCommand.Id` keys the icons in Chrome ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) | wrapper-only |

Nothing on this list required a core change beyond what ADR-0028 introduces (`Density`,
`HeaderHeight`, `Fill`), and nothing required the core to reference a MudBlazor type. That is the
check this ADR exists to record.

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
- **Which Chrome seams `ExGrid.MudBlazor` implements first** is reserved with its trigger: the
  package's own start. The boundary is settled here; the ordering is a product call made with
  the package in hand, and its editor lives inside the fixed box either way (ADR-0028).
