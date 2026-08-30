# Anchor / Focus and keyboard navigation — Enter and Tab cycle inside the selection

Selection is driven by two points: the **Anchor** (fixed end) and the **Focus** (moving end).
Keyboard behaviour follows Excel. **When a range is selected, Enter and Tab cycle inside it and
never leave it.**

## Anchor and Focus

| | Meaning | Moved by |
|---|---|---|
| **Anchor** | the fixed end of range extension | click, Ctrl+click (the start of a new range) |
| **Focus** | where keyboard operations start from; the moving end | arrows, Shift+arrow, Enter / Tab cycling |

- **Click** — Anchor = Focus = that cell. The selection collapses to one cell
- **Shift+click** — Anchor stays; Focus moves to the clicked cell and the range is redrawn
- **Ctrl+click** — adds a new range
  ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
  Anchor and Focus move to the new range
- **Arrows** — collapse the selection to one cell and move
- **Shift+arrow** — Anchor fixed, Focus moves, the range grows or shrinks
- **Ctrl+arrow** — jump to the edge (last / first row vertically, last / first column
  horizontally). Not Excel's "edge of the non-blank block" — query results have no blank rows,
  and finding a block edge would require the whole dataset (ADR-0011)
- **Shift+Ctrl+arrow** — extend the range to the edge
- **Ctrl+Space / Shift+Space** — select the whole column / whole row (as in Excel)

**With disjoint ranges, the most recently created one is the one that grows.** Both Shift+arrow
and Shift+click move only the range the Anchor belongs to.

## Enter and Tab cycling

```
Range B2:D4 selected, Focus at B2

Enter (column-major)          Tab (row-major)

  B2 → B3 → B4 ┐              B2 → C2 → D2 ┐
  ┌─────────────┘              ┌────────────┘
  C2 → C3 → C4 ┐              B3 → C3 → D3 ┐
  ┌─────────────┘              ┌────────────┘
  D2 → D3 → D4 ─→ back to B2   B4 → C4 → D4 ─→ back to B2
```

**Enter runs down columns, Tab runs across rows.** Both wrap at the edge and return to the start
after the last cell. Shift+Enter and Shift+Tab run backwards.

**The range stays selected while cycling**; only the Focus moves. That is what makes "select a
block and just keep typing" work.

With no range (a single cell), Enter moves down and Tab moves right, and the selection follows.

### Why the cycling matters

The two editing modes in
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) exist to make "type, arrow to the
next cell" work. **This cycling is what receives that continuous entry**; implementing only one of
the two achieves nothing.

And **confining entry to the range is a safety device**. Filling five rows with a value works
because, with the range selected, the fifth Enter returns to the top. A design that always moves
down would put the Focus on a sixth row and **edit it without the user noticing**.

Rejected:
- **Always move down / right, ignoring the range** — minimal to implement, but entry spills out of
  the range.
- **Cycle within the range but with Enter and Tab in the same direction** — Excel users
  distinguish the two, so one of them is always going to feel wrong.

## Clicking a column header sorts

**The data-grid convention (click = sort) is adopted; Excel's (click = select the whole column) is
not.** This component is used by people carrying both expectations, so **the tie is broken by
frequency** — what happens first on a data screen is sorting and filtering; selecting a whole
column comes later and occasionally. The frequent operation gets the single click.

Column selection is not lost. **There are three ways.**

- **Ctrl+Space** (Excel's own shortcut)
- **Ctrl+Shift+Down** — extending from the first row to the edge is effectively the whole column
- Clicking the corner above the row-number column (select all)

Rejected:
- **Click = select the column, with sort in the column menu** — sorting becomes two steps. The
  "sort ascending/descending" entries in the column menu
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) would be the only route, and
  opening a menu every time is noticeably heavy.
- **Split the header into regions** (the text sorts, a thin strip selects) — both become single
  clicks, but the hit target is fiddly and users never discover the strip.

## Consequences

- **All of this rides on the capture-phase key handling in
  [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md).** Outside editing, the arrows,
  Enter and Tab belong to the core. While editing, only the arrows change hands according to
  Overwrite / Caret; Enter and Tab are always the core's (commit, then move by these rules).
- **The Focus must always be visible.** If cycling or Ctrl+arrow takes it out of the Viewport, the
  grid scrolls to it.
- **When the Focus leaves the Window, a Range Request is raised**
  ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)). A range taller than the
  Window will move the Focus onto rows that have not been fetched. The Focus cell is a Placeholder
  meanwhile, so **editing does not start until the data arrives**.
- **A change of sort order or filter discards the Anchor and Focus too** (ADR-0011 drops the whole
  selection).
- **With disjoint ranges, cycling visits them in creation order.** Excel also cycles through all
  ranges, but the exact ordering was not verified. Check against Excel during implementation.
- **Excel's detail where Enter after a run of Tabs returns to the starting column is not
  adopted.** The implementation cost outweighs the benefit. Add it if it turns out to be wanted.
