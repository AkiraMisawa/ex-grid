# ExPivot.MudBlazor is a Chrome and a stylesheet, around the grid Wrapper it reuses

*(Proposed 2026-09-30 with [ADR-0058](./0058-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md),
and not yet decided with the user, who asked for "a Wrapper for a MudBlazor-like design". Brought in
line with what was built the same day: the list of controls, and where the words come from.)*

A MudBlazor application should show a pivot that looks like the rest of it: a Material surface, the
palette's colours in both schemes, Roboto, and MudBlazor's own controls in the Field List and its
panels. **`ExPivot.MudBlazor` is that Wrapper**, and it is held to
[ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)'s boundary: it
adapts inwards, owns the palette mapping and its own controls, and never owns geometry, DOM
structure or state.

## Two pieces, and the grid Wrapper reused

```razor
<MudExGridPaper Elevation="1" Dense="true" Hover="true">
    <ExPivot TRecord="Sale" Records="_sales" Fields="_fields" @bind-Layout="_layout"
             PivotChrome="MudPivotChrome.Default" />
</MudExGridPaper>
```

- **The outer element is `ExGrid.MudBlazor`'s `MudExGridPaper`, unchanged.** The report is an
  ExGrid, and the paper already does for it everything ADR-0030 says a Wrapper does for a grid: the
  Material surface, the Visual Tokens mapped onto MudBlazor's palette variables, and the cascaded
  presentation defaults that carry Roboto's glyph widths with the font. ExPivot reads the same
  cascaded value to size its label column, so the font on screen and the widths in the arithmetic
  still leave one hand. A second paper would be a second copy of that obligation.
- **`MudPivotChrome`** is the `IPivotChrome` ([ADR-0060](./0060-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md))
  that draws the Field List, the report filter band, the menus and the panels with MudBlazor's
  controls: `MudCheckBox`, `MudTextField`, `MudSelect`, `MudRadioGroup`, `MudButton`, `MudText`,
  Material icons. It supplies `MudGridChrome` as the report grid's Chrome, so one parameter dresses
  both.
- **`mud-ex-pivot.css`** maps ExPivot's own Visual Tokens — the `±` button, the Field List's
  surfaces and rules, the drop indicator — onto MudBlazor's palette variables, scoped under
  `.mud-ex-grid` as the grid Wrapper's rules are, so load order cannot matter.

**Nothing is forwarded.** There is no `MudExPivot` re-declaring ExPivot's parameters; ADR-0030
recorded why the forwarding sketch was wrong for the grid, and it is wrong here for the same
reasons.

## What the boundary means here

| ExPivot owns | The Wrapper owns | The Wrapper must not |
|---|---|---|
| the rules of every gesture, the Pivot Layout, the drafts, what is dragged | the controls that draw them, and the calls back | decide a move, hold a second copy of the layout, or apply a draft itself |
| where a menu or panel opens and what closes it | the content inside the frame | open a `MudDialog` or `MudPopover` of its own for a surface the core frames |
| the report's grid and its geometry, the label column's width, the indent | the tokens' values | change geometry from CSS, or put a component in a report cell |
| the words, by id | MudBlazor's own localised words where it has them | invent words the core then cannot replace |

- **A `MudSelect` inside Value Field Settings is an Inner Popup** of that panel
  ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)): MudBlazor
  draws its list outside ExPivot's root, through the application's `MudPopoverProvider`, which a
  MudBlazor application already has. Escape closes the list first and the panel next.
- **The words are ExPivot's.** MudBlazor has none for a pivot, so the pane, the menus and the
  panels speak ExPivot's words by id, and the report grid's `MudGridChrome` is handed ExPivot's
  words for its pivot commands ([ADR-0060](./0060-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)):
  one `Label` on ExPivot words the whole pivot, while the grid's own commands keep MudBlazor's
  words where it has them.
- **The `±` button in a label cell is the core's plain markup**, restyled by tokens. A
  `MudIconButton` there would be a component in every painted row — the path ADR-0003 measured and
  ADR-0030 forbids a Wrapper.
- **No JavaScript.** MudBlazor's controls bring MudBlazor's own, as they already do for
  `ExGrid.MudBlazor`'s filter panel; the Wrapper adds none.

## Consequences

- **`ExPivot.MudBlazor` references `ExPivot`, `ExGrid.MudBlazor` and `MudBlazor`**, at exactly the
  versions it was built with for the first two (ADR-0042), and nothing references it but the demo
  pages and its tests.
- **Its tests run MudBlazor's controls in bUnit** and hold the stylesheet to the scoping rule, as
  `ExGrid.MudBlazor`'s tests do; layer 3 runs a MudBlazor pivot page under both schemes.
- **A Consumer on another design system writes an `IPivotChrome` of its own**, or none: the built-in
  markup reads the same Visual Tokens, so a CSS file setting them is a minimal Wrapper, as it is for
  the grid.
