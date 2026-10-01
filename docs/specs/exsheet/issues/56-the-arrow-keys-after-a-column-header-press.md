# 56: The arrow keys after a column header press

Status: ready-for-agent

**What to build:** ADR-0058, "What Part B of the ninth Windows run settled", the fourth bullet (Q52),
which replaces Q48's refusal; and the bullet after it, on a drag that took back what its press
wrote. Excel, pointing at a whole column of another workbook, went with ↓ to the column's first row of
data and with → to the next column (`verification/2026-10-01-windows-9/pointing-scope.md`, cases y1
and y2).

**Blocked by:** None. It shares nothing with ticket 55 but the Sheet's test files; keep to the
Pointing Scope's tests.

- [ ] After a press on a registered grid's column header, ↓ points at the column's first row in the
      grid's current order: the `XLOOKUP(...)` of that row's key is written, replacing `T[<column>]`,
      and the dashes move to its cell. On `/pointing`, `=`, a press on PV's header, ↓ gives
      `=XLOOKUP("R-1", Positions[Id], Positions[PV])`. A first row that has not arrived, or whose key
      is blank or an Error Value, is refused as an arrow reaching it is. The grid scrolls the cell into
      view (SH-35)
- [ ] After a press on a column header, ← and → point at the next column the table has, as a
      column: `T[<that column>]` is written and its body dashed, passing over the grid's columns the
      table does not have. On `/pointing`, ← from PV's header gives `=Positions[Id]`, passing over
      Book; with no such column that way, nothing moves (SH-35)
- [ ] ↑ after a header press is an edge: nothing moves and nothing is told. Shift+arrow and
      Ctrl+arrow are refused as from a cell (SH-35)
- [ ] The grid answers a step from a column (DC-55): down with the column's first row, left or right
      with the nearest named column, as a column; up is an edge. Extend `GridPointedAt.StepAsync` (or
      add its column form) without JavaScript; every public member keeps its XML doc comment
- [ ] `PointingRefusalReason.FromColumnHeader` and its words go, unless something still tells it;
      say which in the comments
- [ ] After a drag took back what its press wrote, the arrows are the Sheet's own Point (`=` ↓ points
      at the cell below the edited one), as Part B read: pin it in Layer 2, so it cannot change
      unnoticed (SH-35)
- [ ] Layer 2 in ExGrid for the column step and in ExSheet for the Scope; Layer 3 in
      `pointing-scope.spec.mjs` on `/pointing`: a header press then ↓, and a header press then ←
      (SH-35)

## Comments
