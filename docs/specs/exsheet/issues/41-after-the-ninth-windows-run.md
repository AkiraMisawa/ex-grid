# 41: The arrow keys point inside a registered grid

Status: ready-for-agent

**What to build:** ADR-0058, "The keyboard", as the ninth Windows run settled it
(`verification/2026-09-30-windows-excel-9/pointing.md`, cases 6, 6x, 7, 7x). Once Excel points into
another workbook, its arrow keys move inside that workbook. After a press on a registered grid, the
Sheet's arrow keys point inside that grid. This ticket first held everything that waited for the run.
The completion items went to ticket 39, and F3 needs nothing built.

**Blocked by:** Tickets 37 (the Scope) and 38 (the dashes)

ExGrid:

- [ ] While pointed at, the grid answers a request for the cell one step from a cell: up or down by a
      row in its current order, or left or right to the nearest of the columns the Consumer names.
      At an edge there is none, and a row that has not arrived (a Placeholder) is answered as such
      (DC-55)
- [ ] Asked to, it scrolls a cell into view, as Point scrolls to its pointed cell (DC-55)
- [ ] No JavaScript is added (DC-55, ADR-0021)

ExSheet:

- [ ] While the text this Point wrote came from a registered grid, ↑/↓/←/→ in the Sheet ask the Scope,
      which asks that grid for the next cell and rewrites the text for it, replacing what this Point
      wrote. The dashes move, and the grid scrolls the cell into view (SH-35)
- [ ] ← and → pass over the grid's columns that the table does not have (SH-35)
- [ ] At an edge nothing moves. A row that has not arrived, Shift+arrow and Ctrl+arrow write nothing,
      leave the text as it was, and tell the reason (SH-35)
- [ ] Layer 2; Layer 3 on both hosts: `=`, a press on R-1's PV, ↓ gives
      `=XLOOKUP("R-2", Positions[Id], Positions[PV])`; → from an Id cell passes over Book to PV; ↓
      past the painted rows scrolls the positions grid (SH-35)
