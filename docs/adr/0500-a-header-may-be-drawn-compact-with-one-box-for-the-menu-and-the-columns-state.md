# A header may be drawn compact, with one box for the menu and the column's state

*Asked for by the user on 2026-11-10 in the project thread "Add a compact display for the header" (its
title and the specification were written in Japanese; they are given here in English). The user's
specification settled the frame: two displays chosen for the
whole grid, `standard` being the header as it is and the default, `compact` needing less width; the state
of a sorted or filtered column visible at rest, never by colour, hover or the menu alone; the meaning of
a header click untouched; widths the user or a saved view set never overwritten; and the choice
independent of Density. It left the design inside that frame to this ADR ("make the ordinary design decisions yourself"), and
asked for two or three designs to be compared. The
comparison is under "Considered". The option's name and the two points marked **for the user** below were
not put to the user before the implementation; they are stated in the pull request, and a different
answer is a rewrite of this ADR.*

*(Rewritten the same day, before the pull request, after a review against the specification: section 4
first had an Auto width measured afresh on a change of display, which let a column its values had
widened come back narrower, a fit nobody asked for. An Auto width now keeps what its values taught it.
What a standard header shows at a width fitted under a compact one, and what gives way in a column too
narrow for both the label and the box, were implied and are now said, in sections 2 and 4.)*

This amends [ADR-0016](./0016-column-width-and-overflow.md) ("What a fit counts in the header"),
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) and
[ADR-0044](./0044-alt-down-opens-one-popover-the-commands-above-the-filter.md) (the ▾ and what it shows)
and [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md) (classes).

## Context

The first Consumer shows risk tables whose columns are short: a currency code, a tenor, a small count, a
flag. Their width is decided by the header, and most of a header's width is not its label.

What a header was charged on 2026-11-10 (`main` at `ebddd66`), at the default Grid Metrics (14px type,
8px cell padding), for a column that can be sorted and has the column menu:

| Part | px | Why it is there |
|---|---|---|
| The label, each character at its class's width | text | ADR-0016's estimate: the grid never measures text |
| The cell's padding, both sides | 16 | as every cell |
| One full-width em of slack | 14 | ADR-0016: proportional letters can run wider than their estimate |
| The menu button's band (16px ▾ and a 6px inset) | 22 | ADR-0010: the label never lies under the ▾ |
| The sort mark's box (an em and a 6px gap) | 20 | ADR-0016: reserved whether sorted or not, so a sort moves no column |

So a header costs **its text and 72px**. `CCY` is 29.25px of text in a 101.25px column whose values need
about 45px. Nothing is reserved for a hover: the ▾ is always painted, and the sort mark's box is always
empty unless the column is sorted. The cost is two boxes side by side, each reserved at every moment for
something that is there only some of the time.

Two more facts the investigation found, which bound the design:

- **The standard header does not say a column is filtered.** The only sign is the menu's *Clear filter*
  being enabled. A design that must show state at rest has to add a mark for it.
- **The header is a string.** There is no header template, so no Consumer's header content has to be laid
  out around; and a level of a multi-column Sort has no number in the header (each sorted column wears ▲
  or ▼ and nothing else), so there is no priority number to keep.

## Decision

### 1. `headerDisplay` chooses how every header of a grid is laid out

```ts
export type GridHeaderDisplay = 'standard' | 'compact';
// GridOptions
readonly headerDisplay?: GridHeaderDisplay;   // 'standard' unless given
```

Blazor's ExGrid takes `HeaderDisplay`, a `GridHeaderDisplay` enum (`Standard`, the default, and
`Compact`), and sends it with its other options. The React adapter and the Wrappers pass it through with
the rest of `GridOptions`. An unknown value is refused by name.

- **`standard` is the header as it was, and its DOM is the one an earlier version drew**: no attribute is
  added to it and no rule of the compact header reaches it. A Consumer who sets nothing sees no change.
- **It is one choice for the grid.** A column has no override: none was asked for, and a header whose
  neighbours lay their marks out differently reads as two grids. A column still declares what it always
  did (`queryable`, `filterUi`, its widths), and `hideColumnMenu` still removes the menu.
- **It moves no meaning and enables nothing.** A click on a header sorts, Shift and Ctrl select, a press
  drags to reorder, the grip resizes, the ▾ and Alt+↓ open the column menu, as before and by the same
  code. What can be sorted, filtered, hidden or pinned is declared as before.
- **It is not Density** ([ADR-0028](./0028-geometry-is-resolved-once-density-is-only-a-preset.md)). Density
  is heights, type size and padding; the Header Display is how many boxes stand beside a label. Each is
  chosen without the other, and every Density resolves both displays. The two words `standard` and
  `compact` are also names of Density presets. They are the user's words for these two displays and are
  kept; the option is named `headerDisplay`, never "compact mode", so that `density: 'compact'` and
  `headerDisplay: 'compact'` are not read as one switch. **For the user:** other value names (`full` and
  `slim`, say) would remove the overlap at the cost of the specification's own words.

### 2. A compact header has one Header Box, inside the cell's padding

Under `compact` the ▾ and the marks of the column's Sort and Filter share **one box**: the menu button's
own box (16px wide, 6px from the cell's right edge), which now stands inside the cell's right padding
instead of beside it.

| The column holds | The Header Box shows |
|---|---|
| nothing | ▾, faint, as the standard header's |
| a Sort | an arrow with a shaft, up for ascending and down for descending, at full strength |
| a Filter | a funnel, at full strength |
| both | a funnel and the arrow, side by side in the same box |

- **Always there, always the same size.** The box is reserved at every moment and painted at every
  moment. Nothing appears on hover or on focus, so no label moves, no target arrives under the pointer,
  and a touch screen and a keyboard meet the same header a mouse does.
- **State is told by shape and by weight, never by colour.** The marks are pictures painted as a mask in
  the header's own colour; the ▾ is at 55% and a mark at 100%. The arrow has a shaft so that a column
  sorted descending is not read as a ▾. Under forced colours the mark is painted in `CanvasText`.
- **A press on the box opens the column menu**, whatever it shows: it is the same button, with the same
  box as the standard header's, so what a press at a given pixel of a header means does not change with
  the display. Sorting stays the header click's; the mark is where the state is read, and the menu is
  where it is changed (*Sort ascending*, *Sort descending*, *Clear filter*, the filter panel).
- **With `hideColumnMenu`** a column that can be sorted keeps the box as a mark and nothing to press: a
  `span` that takes no pointer, so a click there is the header's. A column that cannot be sorted and has
  no menu has no box.
- **For assistive technology** the Sort is the column header's `aria-sort`, as before. The Filter is said
  by the button's name, *Column menu, filtered* (`gridLabelIds.columnMenuFiltered`), or, where there is
  no button, by the mark as an image named *Filtered* (`gridLabelIds.columnFiltered`). Both go through
  `commandLabel`, and the Japanese words are in the bundled table. The button is still not a Tab stop
  ([ADR-0080](./0080-the-keyboard-field-takes-the-keys-of-a-selected-cell.md)); Alt+↓ opens the menu of
  the Focus's column, and closing it returns the keyboard to the grid.
- **A level of a multi-column Sort** shows its own direction on its own column, as in the standard
  header. Neither display numbers the levels.

- **In a column narrower than its header's charge the box stays and the label gives way.** A width
  somebody dragged below the charge is kept (section 4), so the header has to choose: the Header Box is
  painted whole at the right edge, at its own size, and the label is cut with an ellipsis in the room
  left of it, down to no label at all (observed in Chrome at 60px and at the 40px MinWidth: the box is
  16px wide and 6px from the edge at both, with its picture, and a press on it opens the menu). So the order is the state and the way to the menu first, then
  the label; a Sort and a Filter together are one picture in the one box (the funnel and the arrow side
  by side, each smaller than alone), never two boxes, and neither is dropped for the other. A label that
  is cut is read whole by widening the column: a drag of the grip, or Size to fit, which gives the width
  that paints it whole. The header has no tooltip in either display, and this decision adds none.

The compact header does not shrink the type, the ▾ or the button's box, and does not cut the label
sooner: see section 3.

### 3. What a compact header is charged

```
standard:  text + 2·padding + em + menu band (when ▾) + sort box (when sortable)
compact:   text + 2·padding + em + (▾ or sortable ? max(0, menu band − padding) : 0)
```

At the default metrics the box's room past the padding is 22 − 8 = **14px** (10px at Comfortable, whose
padding is 12px), where the standard header's two boxes take 42px.

| Column | standard | compact | saved |
|---|---|---|---|
| sortable, with the menu | text + 72 | text + 44 | 28 |
| not sortable, with the menu | text + 52 | text + 44 | 8 |
| sortable, `hideColumnMenu` | text + 50 | text + 44 | 6 |
| neither | text + 30 | text + 30 | 0 |

The label's room is the cell less its left padding and the box, at every moment. So the guarantee a
standard header gives its label is unchanged: a label that fits its estimate with ADR-0016's em of slack
is painted whole, with or without a Sort, a Filter, a pointer or an open menu. "More labels cut" was not
taken as a saving.

Auto and Size to fit both charge this (`GridMetrics.headerRequiredPx`, which takes the display; the C#
`GridMetrics.HeaderRequiredPx` has the same overload and the shared case holds the two together). Neither
reads the pointer, the keyboard or whether a menu is open.

### 4. Changing the display on a grid in use

The display is an option like the others: a Consumer hands the grid the other value and the grid that
stands takes it.

- **Kept:** the rows and the Grid Source, the Sort, the Filter, the Selection with its Anchor and Focus,
  the scroll position, the column order, the pinned count, an open edit with the text typed so far, and
  an open column menu or filter panel, which stays open on its column.
- **A width somebody set is never changed.** A `Fixed` width, whether declared, dragged, fitted or
  restored from a saved view, stands, and no `onColumnWidthChanged` is raised. A change of display alone
  therefore narrows nothing in a grid whose widths are all set: the room it frees is taken by the user
  with Size to fit (the grip's double-click or the menu), or by the Consumer dropping the widths it
  saved.
- **An Auto width keeps what its values taught it, and takes its header's new need in place of the
  old one.** Auto is the wider of what the header needs and the widest value the grid has seen, and
  only the first of those is a fact of how the header is drawn. So a column its header decided is 28px
  narrower under `compact` and 28px wider again under `standard`; a column its values decided does not
  move in either direction; and the way back is the width the column had, whatever the order the
  settings came in. The same now holds when `hideColumnMenu` changes, which before this decision
  measured the column afresh. Nothing is measured, nothing `Fixed` moves and nothing is reported: the
  change of display is not a fit. *(Rewritten 2026-11-10, see the note at the top: measured afresh, a
  column whose values had grown it under earlier rows came back at the width of the rows now painted,
  which is the implicit fit the specification refused.)* **For the user:** the specification asked
  that a change of display should not become an implicit fit of the columns, and allowed the table to
  narrow where the Consumer's own policy says so. An Auto column is that policy, declared by the
  Consumer, so its header's part follows the display. If Auto columns were meant to hold their width
  too, they could keep it on the way to compact and grow only on the way back, at the cost of a grid
  whose widths depend on the order of its settings.
- **Back to `standard`, a header is the standard one again, at the widths that stand.** Nothing is
  dropped from it by rule: the ▾ is in its band and the sort mark in its box. What it can lose is room.
  A `Fixed` width that was fitted under `compact` is 28px short of the standard charge, and the standard
  header does there what it does in any column narrower than its charge. The ▾ stays. The label keeps
  6px of its slack, so a label that is not sorted is usually still whole; the sort mark's 20px no longer
  fit, so once the column is sorted an ellipsis stands where the mark would (`Tenor…`, observed in
  Chrome on `/header-display`), and the Sort is then read from `aria-sort` by assistive technology, or
  on screen after the column is widened. The column menu does not say which way a column is sorted, in
  either display. The Filter has no mark in a standard header at any width (see Consequences). The
  user's way out is the one that made the width: Size to fit under `standard` gives the standard
  charge. The grid does not widen the column for them, because that width was set.
- **All of this is said of a column the Consumer hands again as the same object.** An Auto width is
  carried for the same column object under the same Grid Metrics, and measured afresh otherwise
  (ADR-0016, ADR-0003's note of 2026-10-11): that rule is older than this decision and is not changed
  by it. A Consumer who builds its columns anew on every render already has them measured afresh on
  every render, the one that changes the display included. Blazor's adapter keeps the objects.

### 5. Nothing new crosses to the browser's layout, and nothing per cell

The marks are stylesheet pictures chosen by attributes the core writes when it paints the header
(`aria-sort`, which it already wrote, and `data-ex-filtered`), and the band wears
`data-ex-header-display="compact"`. No layout is read, no listener is added, a hover needs no script and
no round trip on Blazor Server, and the header is repainted only when its key changes, to which the
display and the filtered columns were added.

## Considered

Three designs for `compact`, at the default metrics, for a sortable column with the menu (standard:
text + 72px).

| | A. One reserved box (**taken**) | B. The box inside the slack, ▾ on demand | C. No entry in the header |
|---|---|---|---|
| Header's cost | text + 44 (−28) | text + 30 (−42) | text + 30 (−42) |
| Entry to the menu | ▾ always painted | ▾ on the band's hover, on the Focus's column, always on touch | context menu, Alt+↓, a toolbar the Consumer builds |
| Finding it | as today | learned by moving the pointer | poor: nothing to see |
| State at rest | in the box | in the box, over the label's slack | needs its own mark and room, or the label's |
| Label | same guarantee as standard | a label wider than its estimate is cut or painted under the mark | same as B once a mark is added |
| Stability | nothing moves | the ▾ appears under the pointer; a right-aligned header needs the box mirrored | nothing moves |
| Keyboard, touch | unchanged | touch needs its own always-on rule | touch needs new Chrome |
| Cost to build | small: one option, one metric, CSS | middle: hover and Focus rules, mirrored layout | large: a new entry and its Chrome in three Wrappers |

- **B** saves 14px more by spending the em of slack ADR-0016 gave the label. ADR-0016 measured `Amount`
  3.2px and `MARKET VALUE` 6.9px past their estimates; a 16px mark in a 14px slack leaves
  those labels cut or overlapped, and `CCY` in a column fitted to its header could read `C…`. That is
  the saving the specification refused. It also makes the ▾ a thing that arrives under the pointer.
- **C** saves the same 14px by removing the entry the user sees. The state still needs a mark, so the
  saving is B's with B's cost to the label, and the menu's only pointer route becomes a right-click,
  which a touch screen does not have.
- What is left in A, text + 16 + 14 + 14, is the padding, ADR-0016's slack and the one box. The slack
  could go only by measuring the label, which the grid does not do
  ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)); the padding is Density's.

## Consequences

- A table of ten short columns measured in the React DemoHost (`/header-display`, Chrome, the default
  Density) is 1331px wide in `standard` and 1095px in `compact` on Auto widths: 236px, or 17.7%, with
  seven header-decided columns 28px narrower each and no label cut.
- A compact header shows that a column is filtered and a standard header still does not. The standard
  header was left as it was, because adding a mark to it changes every grid in use; whether it gains one
  is a decision of its own.
- The label's em of slack is now most of what a compact header carries beside its text. Removing it is
  not this decision.
- Touch ([ADR-0045](./0045-exgrid-is-a-desktop-grid.md),
  [ADR-0260](./0260-touch-input-reports-browser-facts-and-keeps-command-meaning-in-csharp.md)) gains
  nothing and loses nothing: the box is the ▾ a tap already opens, at the size it had.
- ExSheet and ExPivot draw their grids without the column menu and do not pass the option, so their
  headers are the standard ones they were.
