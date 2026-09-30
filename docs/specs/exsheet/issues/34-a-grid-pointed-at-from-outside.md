# 34: A grid pointed at from outside hands its presses on

Status: ready-for-agent

**What to build:** ExGrid's half of ADR-0058, "While a Sheet points" and "What is drawn". ExGrid
gains a declaration, made by any Consumer, that the grid is pointed at. While it is, a press does not
act, and is handed over. The grid also draws dashes where it is told. ExGrid learns nothing about
Formulas, Sheets or Linked Tables. ExSheet's Pointing Scope (ticket 37) is the Consumer that
declares it.

**Blocked by:** None (can start immediately)

- [ ] A declaration, off by default, that the grid is pointed at, with a callback that receives each
      press handed over. Without it nothing changes (DC-1, DC-52)
- [ ] While declared, a primary press on the rows moves neither DOM focus nor the Selection and the
      Focus. The rows' `@onmousedown:preventDefault` already takes a bool (`PressKeepsTheEditor`,
      `ExGrid.Pointing.cs`); it becomes true while pointed at as well (DC-52)
- [ ] While declared, a press on a column header moves no DOM focus and runs no sort, column menu,
      reorder or Heading drag (`OnHeaderMouseDown`, `OnHeaderClickAsync`, the menu button). The header
      has no `preventDefault` today (DC-52)
- [ ] The hand-over carries what was pressed: one cell (the row's identity and the column's name), a
      column header (the column's name), or a shape the Consumer will refuse: more than one cell
      (Shift+press, or a drag across cells) or a Header Group's rectangle. The grid does not decide
      what is written or refused (DC-52)
- [ ] `ex-pointed-at` joins the root while declared, and the stylesheet makes the pointer `cell` over
      the rows and headers (ADR-0029's note of 2026-09-30, DC-52)
- [ ] Dashes when asked: a cell (a row's identity and a column) or a column, drawn as one
      `ex-point-dashes` element in the selection overlay, in `--ex-focus-outline`, cut to the painted
      rows as a Reference Outline is. A row that is not painted draws nothing and is not scrolled to.
      After a reorder the dashes are over the same row (DC-53)
- [ ] Column outlines asked for through the same declaration are drawn as `OutlinedColumns` draws
      them, together with any the page passes itself (ADR-0057, "A column the Consumer asks to
      outline twice is outlined twice")
- [ ] Layer 2: a press while declared, on a cell, a header and a Header Group, and with Shift. Assert
      the hand-over, and that the Selection, the sort and DOM focus do not change. Dashes by identity
      across a reorder. Nothing without the declaration (DC-52, DC-53, DC-1)
- [ ] No JavaScript is added by this ticket (ADR-0021); ticket 35 adds the one event
