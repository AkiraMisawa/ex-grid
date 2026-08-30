# Action and Template columns are separate. Space engages the cell; Enter always moves

Columns that put custom content in cells come in **two kinds**.

| | Declared as | Render cost | Keyboard |
|---|---|---|---|
| **Action Column** | "cells in this column carry N actions", plus an icon or label from the Consumer | **plain markup; adds no boundary** | N=1 fires on Space; N≥2 enters the cell on Space |
| **Template Column** | an arbitrary `RenderFragment` | **the cell becomes a component** | enters the cell on Space |

The grounds for splitting them are the rule from
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) itself — **Chrome renders, the core
decides meaning**. "Pressing this makes something happen" is meaning, so the core holds it; the
icon's appearance comes from the Consumer.

## Performance: the cost is the number of boundaries, not the complexity of the content

What was expensive in the
[ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md) measurements was **800
per-cell component boundaries**.

```
Action Column    a <button> painted as plain markup inside the row component
                 → zero new boundaries. stays on the 1.90 ms (800 cells) path

Template Column  a RenderFragment is effectively a component
                 → boundaries increase. specified on every column, it heads toward
                   the measured 92.0 ms (2000 cells, fling)
```

Key input is already captured by the core in the capture phase
([ADR-0018](./0018-multiple-instances-must-be-independent.md)), so **no per-cell handler is
needed** either. Clicks can be taken once on the root and resolved from `data-` attributes. (In
fairness, around 40 visible cells means per-cell handlers would not be fatal — **it is the
componentisation that costs**.)

## Keyboard — one rule

```
Enter / Tab   navigation. ALWAYS moves; never fires anything
Space         engages with the cell's content
Esc           leaves
F2            enters Caret mode on an editable cell
```

**Space's meaning follows naturally from the kind of cell.**

| Cell kind | Space |
|---|---|
| Editable | enter Overwrite mode (as if a space were typed; ADR-0010) |
| Action (one action) | **fire it** |
| Action (several) | enter the cell. Arrows to choose, Space to fire, Esc to leave |
| Template | enter the cell |

All of these read as "engage with this cell's content", with no exceptions to remember. Space is
also the native activation key for a `<button>`, so keyboard users' fingers already know it.

### Why Enter does not fire as well

[ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md) made Enter the key that cycles
column-major inside the selection. If Enter also fired in an Action Column, **an attempt to move
would chain.**

```
Holding Enter to move down a column of delete actions
  → delete row 1, move down → delete row 2, move down → …
```

Enter is a key people hold down to move, so accidents are easy. It departs by half from the web
convention (buttons also respond to Enter), but **losing vertical traversal with Enter is lighter
than the misfire**. Hunting up and down a narrow column for the row to open is an ordinary thing
to do.

Rejected:
- **Fire on both Enter and Space** — natural as a button, but the chaining risk above.
- **Space always fires; Enter fires only for non-destructive actions** — the accident is
  prevented, but **Enter's meaning varies per action** and users cannot predict it.

## With several actions, consider separate columns first

```
[Open] [Copy] [Delete]     ← three Action Columns side by side

→ left/right arrow movement IS the movement between buttons
→ Space fires. NO mode to enter
```

It reuses the horizontal cell navigation the grid already has, so **no new concept and the fastest
keyboard path**. The downsides are a row of narrow columns and empty headers.

**With two or three, split the columns first; if that is unacceptable, pack them into one cell and
use the Interactive mode.**

## "How many actions" is declared, never inferred

Space means something different at N=1 and N≥2, but **N is the number the Consumer declared**, not
a count of focusable elements in whatever markup Chrome painted.

```
❌  count the focusables inside Chrome's markup
      → the count can differ between the default and MudBlazor implementations
        = behaviour depends on the implementation

✅  read the number of actions the Consumer declared
      → the core knows it. whatever Chrome paints, it does not change
```

ADR-0010's "Chrome does not decide meaning" is preserved.

## Four rules that always accompany a Template Column

- **A template is rendering only; the value accessor (`Column.Get`) is still required.** Template
  columns still need sorting and filtering. **This is a hole MudBlazor's `MudDataGrid` actually
  fell into** — Consumers had to walk `RenderedColumns` to map column GUIDs back to property names
  ([ADR-0009](./0009-filter-panel-contract.md)). A column with no value (actions only) is
  **declared unsortable and unfilterable**.
- **Copy does not use the template.** It uses `Column.Get` and the format
  ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)). Otherwise **a button's HTML ends up
  pasted into Excel**.
- **`####` does not apply to template columns**
  ([ADR-0016](./0016-column-width-and-overflow.md)). Overflow handling is about cells that paint a
  value; whether a template's content fits is the Consumer's responsibility.
- **Hold the template on the column definition; do not construct it per render.** A new lambda
  each time makes the `Column` look changed and **slips past row memoisation** — the same trap as
  "cache delegates in a field" in ADR-0003.

## Consequences

- **There are now three modes.** Selected / Overwrite–Caret (editing) / Interactive (inside a
  cell). All of them ride on the capture-phase key handling (ADR-0010).
- **Interactive is the accessible standard form.** The ARIA grid pattern is exactly "the grid is
  one tab stop, with an operation to enter a cell", and this matches it.
- **What a detail action opens happens outside the grid.** Expanding inside the row is not
  possible ([ADR-0013](./0013-fixed-row-height.md), fixed row height). Either navigate to another
  view, or expand a group so that rows increase. The grid only reports that it was pressed; the
  Consumer holds the state.
- **Action Column headers tend to be empty.** Whether the column menu (ADR-0010) and width
  handling should be identical to a value-bearing column is worth revisiting during
  implementation.
