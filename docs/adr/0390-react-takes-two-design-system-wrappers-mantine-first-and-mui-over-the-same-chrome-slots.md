# React takes two design-system Wrappers, Mantine first and MUI, over the same Chrome slots

*Decided with the user on 2026-10-26 in the project thread "MUIとMantineのどちらで作るか決める". The user
asked whether the Wrapper planned as `@ex-grid/mui` should be built on Mantine instead, then decided both
are supported with Mantine first ("優先順位をmantineの方を高めにしておいて。どっちも対応する予定"), and,
offered 0.3.0 with Mantine alone, both, or neither, chose both. Section 4's order, section 3's split and
section 5's refusal of a MUI theme without CSS variables were proposed with it and are this ADR's
reading; the open questions at the end were not decided.*

This amends [ADR-0350](./0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md)
sections 9, 11 and 12 and
[ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) sections 5, 6 and
10, which named MUI as React's only design system.

## Context

ADR-0350 section 11 made `@ex-grid/mui` React's counterpart of `ExGrid.MudBlazor`, and ADR-0370 gave
ExSheet, ExPivot and ExChart a `-mui` Wrapper each. None of them was started. Mantine is the other React
design system a Consumer of a grid family is likely to bring, and its theme reaches the page in the form
[ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)'s MudBlazor Wrapper
already takes.

What the code showed on 2026-10-26 (`main` at `32334b1`):

- **The Script Cores have no Chrome slot yet.** `@ex-grid/core` draws its column menu, Context Menu,
  filter panel and find panel itself (`dom/menu-popover.ts`, `filter-panel.ts`, `find-panel.ts`), and
  `@ex-grid/react` exposes `templateColumn` alone. ADR-0350 fixed the slot's shape, a mount function
  `(slot, context) => cleanup` in the core and a render function drawn through a portal in React, but no
  slot exists. ExSheet's, ExPivot's and ExChart's Script Cores are the same. Every design-system Wrapper
  waits on this, whichever design system it is.
- **The popover's frame is the core's.** Where a menu stands, how wide it grows, what closes it and what a
  key means in it are the core's (ADR-0010, ADR-0039, ADR-0040). `ExGrid.MudBlazor` uses no `MudMenu`: it
  puts `MudButton`s with `role="menuitem"` inside the core's frame. A design system's own positioning
  (Mantine's Floating UI, MUI's Popper) is therefore not used inside the grid, and what differs between
  two Wrappers is their components' look and how their theme reaches the stylesheet.

How the two theme the page (Mantine 9.7.4 and MUI 9.5.0, the versions on npm on 2026-10-26):

| | Mantine 9 | MUI 9 |
|---|---|---|
| Styling | CSS Modules, no CSS-in-JS at run time | Emotion at run time by default; Pigment CSS is an alpha on hold ([MUI, "2026 and beyond"](https://mui.com/blog/2026-and-beyond/)) |
| Theme on the page | `MantineProvider` always writes `--mantine-*` on `:root`; the scheme is the `data-mantine-color-scheme` attribute | A JavaScript `theme` object; `--mui-palette-*` exists only under `createTheme({ cssVariables: true })` |
| Against `ex-grid.css` | `styles.layer.css` puts all of it in `@layer mantine`, which an unlayered stylesheet always beats | Emotion inserts its `<style>` at run time; the order is `StyledEngineProvider`'s |
| Words | Its components carry no localised text | Locales such as `jaJP` |

Measured the same day: the components a Wrapper would use (menu items, popover, dialog, tabs, buttons,
text field, checkbox, select, tooltip, alert, paper) bundled by esbuild for production, less a bundle of
React 19 alone, weigh about 91 KB gzipped under Mantine (77 KB of script and 14 KB of the components'
own CSS) and about 92 KB under MUI, Emotion included. Size decides nothing between them.

## Decision

### 1. Two Wrapper sets, named after their design system

| Product | Mantine | MUI | Blazor |
|---|---|---|---|
| ExGrid | `@ex-grid/mantine` | `@ex-grid/mui` | `ExGrid.MudBlazor` |
| ExSheet | `@ex-grid/sheet-mantine` | `@ex-grid/sheet-mui` | `ExSheet.MudBlazor` |
| ExPivot | `@ex-grid/pivot-mantine` | `@ex-grid/pivot-mui` | `ExPivot.MudBlazor` |
| ExChart | `@ex-grid/chart-mantine` | `@ex-grid/chart-mui` | `ExChart.MudBlazor` |

ExBoard has no Wrapper, as ADR-0370 section 5 has it. Every one of them is held to ADR-0030 as written:
it maps its palette to a Theme and its density words onto Density, supplies Cell Metrics for its font,
fills the Chrome slots and adds its outer chrome, and owns no geometry, no DOM inside the instance root
and no state.

*Note of 2026-10-28, as React's ExBoard took the kits' Chromes:* a Board is dressed by its products'
Wrappers rather than one of its own. `BoardOptions.chrome` holds a Chrome for each kind of view, `grid`,
`pivot` and `chart`, handed to every view of that kind, and the grid's dresses the pivots' report grids where
`pivot` names no grid of its own, as the Razor ExBoard's `Chrome`, `PivotChrome` and `ChartChrome` do. React's
`ExBoard` takes them as render functions through its own portals, so an application hands it
`mantineChrome`, `mantinePivotChrome` and `mantineChartChrome` (or MUI's) inside an element of the Wrappers'
classes, whose Visual Tokens map the views' colours onto the theme, as each Wrapper's frame documents for a
Consumer that frames a product itself.

### 2. Both ship in 0.3.0, and Mantine's comes first

ADR-0370 section 6's gate gains the eight Wrappers: 0.3.0 is not tagged until each runs stably on the
React host as the products do. Where the two sets compete for the same time, Mantine's is built first.

### 3. They share what lies below a design system, and nothing above it

- **Each Script Core** holds its Chrome slots and what a slot is handed: the commands in the core's order,
  each Enabled or not, their words, what a key means (the `MenuKeys` table), and where the frame stands.
  A slot left unfilled is drawn by the built-in Chrome, as today.
- **Each React adapter** (`@ex-grid/react`, `sheet-react`, `pivot-react`, `chart-react`) takes a `chrome`
  prop of render functions, one per slot, drawn through a portal into the slot the core made, and exports
  the hooks a Wrapper needs to stay thin: answering a key inside a menu by the core's table, handing DOM
  focus to an editor control the Wrapper drew (ADR-0030), and reading the words. A Consumer's in-house
  design system takes the same prop and the same hooks.
- **Each Wrapper** holds its design system's components in those slots, the stylesheet that maps the
  `--ex-*` tokens onto its design system's variables, its Density mapping, its Cell Metrics and its outer
  chrome (its paper, ExChart's Chart Dock).

No layer of design-system-neutral buttons and menus sits between the React adapters and the Wrappers. It
would be a small design system of its own, and each Wrapper would fight its limits rather than its own
design system's.

### 4. The order

1. ExGrid's Chrome slots in `@ex-grid/core` and `@ex-grid/react`, the seams `IGridChrome` has in C#:
   the column menu, the Context Menu, the filter panel, the find panel, the cell editor, a cell's message,
   the loading indicator, the Selection Summary, the Name Box, the Formula Bar's text and the editor's
   completion.
2. `@ex-grid/mantine`, with the Docs Site's React Examples switching to it.
3. Each product's Chrome slots and its `-mantine` Wrapper, the three products in parallel. ExSheet, ExPivot
   and ExChart are drawn by ExGrid, so they take ExGrid's slots from step 2.
4. The `-mui` set, over the same slots and hooks.

### 5. A theme reaches the grid through CSS variables

Each Wrapper's stylesheet maps the Visual Tokens onto its design system's variables, as `mud-ex-grid.css`
does onto MudBlazor's: a palette edit or a change of scheme is the browser recomputing variables, with no
render ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)).

- **Mantine's** maps onto `--mantine-*`, and follows `data-mantine-color-scheme` for dark.
- **MUI's** requires a theme made with `cssVariables: true`, and maps onto `--mui-*`. Under a theme
  without them the MUI Wrapper refuses to mount, naming the option, rather than draw the grid in colours
  that are not the Consumer's theme. Computing the tokens from the theme object in script instead would
  render on every theme change, the path ADR-0030 left behind.

### 6. Words

A Mantine Wrapper's words are the family's tables and the Consumer's label function (ADR-0370 section 10),
since Mantine's components carry none of their own. A MUI Wrapper takes MUI's localised text where MUI has
a key, as ADR-0370 section 10 says.

### 7. Tests

What every Wrapper must do with a slot is one contract, run at layer 2 against each Wrapper alike: the
core's commands in the core's order, each Enabled as the core says, a key answered by the core's table,
and nothing decided by the Wrapper. Layer 3 runs, per design system, only what a browser shows: its theme
painted in light and dark, a menu crossed by the keyboard, and the geometry unchanged under its
stylesheet. The products' specs are not copied per Wrapper
([ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) section 11).

### 8. The Docs Site

Under React the Chrome switch reads Built-in / Mantine / MUI (amending ADR-0350 section 12 and
[ADR-0110](./0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)'s
framework switch). It changes when step 2 of section 4 gives an Example a Wrapper to run under.

### 9. `@ex-grid/mantine`'s three answers

*Decided 2026-10-27 with `@ex-grid/mantine`, on the recommendation put to the user, under the user's standing
approval of recommendations ("推奨で"). These were this ADR's open questions for Mantine; a different answer from
the user rewrites this section.*

- **Density.** The Density preset picks the size Mantine's components are drawn at in the grid's Chrome: Compact
  and Excel `xs`, Standard `sm` (Mantine's own default), Comfortable `md`. The size travels from `MantineExGrid`
  through React's context, which a portal keeps; the grid's geometry stays the preset's (ADR-0027/0028). Mantine's
  Table words `striped` and `highlightOnHover` map onto Row Stripes and the hover band as presentation defaults,
  which a value the grid is given beats (ADR-0030).
- **Cell Metrics.** The cells are drawn in Mantine's default stack written out literally (`-apple-system`,
  `BlinkMacSystemFont`, `Segoe UI`, `Roboto`, `Helvetica`, `Arial`, `sans-serif`), not in `--mantine-font-family`,
  because a theme's font is one the grid has no widths for and `####` would be judged by the wrong face. The
  widths handed with it are the widest any face of the stack was measured at: macOS's system face, Segoe UI under
  an English Windows display, Roboto, Liberation Sans for Helvetica and Arial on Linux, and DejaVu Sans
  (`tests/GlyphWidths`, which writes `@ex-grid/mantine`'s table). A Consumer who wants the theme's font hands
  `MantineExGrid` a `font` with its family and its measured widths.
- **Dates.** No `@mantine/dates`, and no dayjs: a date is typed, and read by the core's operand reading, as the
  built-in Chrome reads it.

### 10. `@ex-grid/mui`'s answers

*Decided 2026-10-27 with `@ex-grid/mui`, on the recommendation, under the user's standing approval of recommendations
("推奨で"), as section 9 was for Mantine. It answers the question this ADR left open on MUI's dates, and asks of MUI
the three that section 9 answered for Mantine; a different answer from the user rewrites this section.*

- **Dates.** No `@mui/x-date-pickers` and no date library's adapter: a date is typed, and read by the core's operand
  reading, as under Mantine and the built-in Chrome. The pickers would make every Consumer take a date library the
  grid does not otherwise need, and a typed date reads the same in all three Chromes.
- **Density.** MUI's components take `small` or `medium`, and its lists a `dense` word: Compact and Excel draw them
  `small` with dense menus, Standard and Comfortable at MUI's own default, `medium`, since MUI has no larger field.
  The size travels from `MuiExGrid` through React's context, as Mantine's does; the grid's geometry stays the preset's.
- **Cell Metrics.** The cells are drawn in MUI's default stack written out literally (`"Roboto", "Helvetica",
  "Arial", sans-serif`), not in the theme's typography, for the reason section 9 gives. The widths are the widest any
  face of the stack was measured at: Roboto (`RobotoGlyphTable`'s records), Liberation Sans for Helvetica and Arial
  (the faces macOS and Windows draw those names in have the widths Liberation Sans was made to match), DejaVu Sans
  for `sans-serif` on Linux, and every platform's system face, Japanese Windows' included, which stands in for the
  fallback that paints a glyph Helvetica or Arial lacks: neither of those two is measured itself.
  `tests/GlyphWidths` writes `@ex-grid/mui`'s table from them.
- **The theme.** Section 5's refusal reads the theme `useTheme()` answers: one made without CSS variables has no `vars`
  (MUI's ThemeProvider writes `null` there), and `MuiExGrid` throws, naming `createTheme({ cssVariables: true })`. A
  theme whose `cssVarPrefix` is not MUI's own `mui` is refused too, naming the option, since the stylesheet reads
  `--mui-*`. The variables must also reach the Paper: a `rootSelector` naming an element the grid stands outside
  leaves them unset there, and that is said on the Docs Site, not checked, because checking it is a read of computed
  style the Wrapper does not make. The Paper is `outlined` unless asked otherwise: MUI lightens an elevated Paper
  under the dark scheme with an overlay image the grid's opaque ground cannot take. The
  stylesheet reads only variables MUI writes again for each colour scheme, so it names no scheme selector and holds
  under any `colorSchemeSelector`. The Focus outline is the primary's main shade, which MUI's default dark scheme writes
  lighter, as MUI draws its own focused fields.
- **Words.** MUI's localised text has no key for any word the grid shows (its keys are the Autocomplete's,
  Pagination's and the like), so section 6 leaves MUI's words where Mantine's are: the family's tables and the
  Consumer's label function.
- **Menus.** MUI's `MenuItem` stands only inside its `MenuList`, which answers the arrows itself, so a menu's item is
  MUI's `ListItemButton`, and every key stays the core's table.

### 11. `@ex-grid/sheet-mantine`'s answers

*Decided 2026-10-27 with `@ex-grid/sheet-mantine`, on the recommendation, under the user's standing approval of
recommendations, as sections 9 and 10 were; a different answer from the user rewrites this section.*

- **Format Cells' frame.** Under React, Format Cells' contents are a Chrome's and its frame is the grid's popover,
  the one the built-in Format Cells stands in, under every Chrome: the frame, its place, its Escape and the keyboard's
  return are the core's (section 3). `MudSheetChrome` draws a page-level `MudDialog` in C#; a Mantine `Modal` would
  need the Sheet to put a surface outside its root in the Input Order, the seam C#'s `JoinSurface` is, which the
  TypeScript Sheet does not have. The grid's popover tells the opener once it closes, however it closes
  (`openPopover`'s `closed`), so the contents are taken away with it.
- **The lists.** A Toolbar Item's list is Mantine's `Popover`, drawn inside the Sheet, not in a portal, so the Sheet
  hears its keys and its presses keep their place in the Input Order (ADR-0240), and fixed to the viewport, so the
  toolbar's clipping does not cut it. Its keys are the built-in list's (ADR-0100), not Mantine's.
- **The cells.** The Chrome is set in Mantine's font; the cells keep the Sheet's, because a Sheet's column widths are
  counted in its characters (ADR-0027), and the Workbook's Paper and Ink, which no scheme maps (ADR-0071). The Density
  picks the components' size as section 9 says.
- **The Docs Site.** An Example is written for the Wrappers its product has: ExSheet's for Mantine alone, until it
  has a MUI Wrapper (section 12 gives it one). Where the reader's Chrome has no Wrapper for the product, the built-in
  Chrome runs and the Example's toolbar says so, rather than run the built-in Chrome under that Wrapper's name
  (section 8).

### 12. `@ex-grid/sheet-mui`'s answers

*Decided 2026-10-27 with `@ex-grid/sheet-mui`, on the recommendation, under the user's standing approval of
recommendations, as sections 9 to 11 were; a different answer from the user rewrites this section.*

- **Section 11 holds under MUI.** Format Cells' contents stand in the grid's popover, the cells keep the Sheet's
  font, Paper and Ink, and the Density picks the components' size as section 10 says. A theme without CSS variables
  is refused by name, as `MuiExGrid` refuses it, since the Sheet's Chrome would otherwise be drawn in colours that are
  not the theme's.
- **The lists.** A Toolbar Item's list is MUI's `Popper` on a `Paper`, inside the Sheet, not in a portal, and fixed
  to the viewport, for section 11's reasons. MUI's `Popover` and `Menu` are not used: both are modal, holding the
  keyboard and laying a backdrop over the page, and `Menu`'s `MenuList` answers the arrows itself. A choice is MUI's
  `ListItemButton`, as section 10 draws a menu's item.
- **What is kit-neutral is shared, not copied.** The list's keys, its rows, its opening and closing on the toolbar's
  requests and on a press outside the item, and the props every control takes were written into
  `@ex-grid/sheet-mantine`; they move into ExSheet for React as `useToolbarItem`, as the grid's menu keys live in
  `useMenuItems` (section 3), and both Wrappers draw with it. The outside press is heard from the commit that opens
  the list, so a press made at once after the opening closes it too (the spine's sixth rule), and Mantine's own
  dismissal is off. Format Cells' OK over a form whose field is left invalid moves into `useFormatCells`, and a
  palette's colour for its target into the Sheet's draft (`colourOf`, `setColour`), which the built-in Format Cells
  uses too. The theme check is `@ex-grid/mui`'s `refuseThemeWithoutVariables`, as every MUI
  Wrapper's is. A Consumer's own Chrome takes the same hooks.
- **The Docs Site.** ExSheet's React Examples are written for both Wrappers.

## Open

- **MUI's dates** were answered in section 10.

## Consequences

- 0.3.0 carries eight Wrappers instead of four, and ADR-0370's estimate of "three MUI Wrappers written"
  becomes six, beside the two for ExGrid.
- The Chrome slots, which no Wrapper can start without, are now the first step of React's design-system
  work, for both sets.
- Mantine is maintained largely by one person; MUI is maintained by a company. Supporting both means a
  Consumer is not left without a design system if either stalls.
- A third design system (a Consumer's own) takes the same slots and hooks and needs nothing from either
  Wrapper.
