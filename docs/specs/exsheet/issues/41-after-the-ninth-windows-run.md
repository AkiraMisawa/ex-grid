# 41: The arrow keys point inside a registered grid

Status: done

**What to build:** ADR-0058, "The keyboard", as the ninth Windows run settled it
(`verification/2026-09-30-windows-excel-9/pointing.md`, cases 6, 6x, 7, 7x). Once Excel points into
another workbook, its arrow keys move inside that workbook. After a press on a registered grid, the
Sheet's arrow keys point inside that grid. This ticket first held everything that waited for the run.
The completion items went to ticket 39, and F3 needs nothing built.

**Blocked by:** Tickets 37 (the Scope) and 38 (the dashes)

ExGrid:

- [x] While pointed at, the grid answers a request for the cell one step from a cell: up or down by a
      row in its current order, or left or right to the nearest of the columns the Consumer names.
      At an edge there is none, and a row that has not arrived (a Placeholder) is answered as such
      (DC-55)
- [x] Asked to, it scrolls a cell into view, as Point scrolls to its pointed cell (DC-55)
- [x] No JavaScript is added (DC-55, ADR-0021)

ExSheet:

- [x] While the text this Point wrote came from a registered grid, ↑/↓/←/→ in the Sheet ask the Scope,
      which asks that grid for the next cell and rewrites the text for it, replacing what this Point
      wrote. The dashes move, and the grid scrolls the cell into view (SH-35)
- [x] ← and → pass over the grid's columns that the table does not have (SH-35)
- [x] At an edge nothing moves. A row that has not arrived, Shift+arrow and Ctrl+arrow write nothing,
      leave the text as it was, and tell the reason (SH-35)
- [x] Layer 2; Layer 3 on both hosts: `=`, a press on R-1's PV, ↓ gives
      `=XLOOKUP("R-2", Positions[Id], Positions[PV])`; → from an Id cell passes over Book to PV; ↓
      past the painted rows scrolls the positions grid (SH-35)

## Comments

2026-10-01, implemented on `agent/pointing-scope-41`.

- **ExGrid (DC-55).** The declaration answers for the grid it is passed to (the grid binds itself as
  it takes it, and lets go when disposed or given another). `GridPointedAt.StepAsync(isRow, column,
  direction, isColumn)` answers a `GridPointedStep<TRow>`: `Cell` (row instance and column), `Edge`,
  `RowNotArrived` (a Placeholder, with the column) or `NotHeld` (no row it holds is the one named, no
  column of that name, or not pointed at). ↑/↓ move one row in the current order over the whole
  result, pages included; ←/→ go to the nearest column `isColumn` accepts. The row is named by a
  predicate, as `GridPointDashes.OverCell` names it, so it is found through a sort and through a
  Window of new instances: the painted rows are asked first, then the rest of the Window. The step
  moves no Selection and no scroll. `GridPointedAt.RevealAsync(isRow, column)` scrolls the cell into
  view through the reveal Point uses, turning the page under a `PageSize`; `StageReveal` now reveals a
  target in a grid with no Selection of its own.
- **The keys.** A new parameter, `ExGrid.OnPointArrowFromOutside` (`EventCallback<GridPointArrow>`,
  with `Direction`, `Extends` for Shift and `ToEdge` for the Primary Modifier), hears the arrows while
  the text Point wrote was written from outside. `OnEditingKeyAsync` hands them on just before
  `OnPointKey`, and awaits the Consumer, so the keys typed after an arrow are held behind it
  (ADR-0010's hold; layer 3 checks it with 150 ms injected). `OnPointKey` is unchanged but for its
  comment: Home and End, and the arrows where no Consumer hears them, still move nothing. No listener,
  interop call or layout read is added. The one change in `ex-grid.js` is a set in the key gate:
  `pointedKeys`, Point's keys and the Primary Modifier's arrows with Shift or without, claimed while
  `GateMode()` says `pointed` (Point written from outside, and a Consumer that hears the arrows).
  Without it Ctrl+arrow would reach the field and move the caret, and could not be told. On a circuit
  the gate hears `pointed` a message after the render of the press, so a Ctrl+arrow within that round
  trip reaches the field; it moves the caret and ends Point, and writes nothing.
- **ExSheet (SH-35).** `PointingScope.PointArrowAsync` moves from the cell the dashes name. ←/→
  accept the grid columns the pointing Sheet's table has, through the registration's correspondence.
  A cell reached is written as a press on it is (the cell lookup is now shared between the two),
  replacing what this Point wrote; the dashes move, and the grid is asked to reveal it. The press's
  drag no longer takes anything back after an arrow. Refusal reasons: Shift+arrow is `SeveralCells`
  with its own message; Ctrl+arrow (and Ctrl+Shift+arrow) is a new `DataEdge`; a row not arrived is
  `RowNotArrived`; a blank or Error key reached is refused as a press there is, and nothing moves; a
  cell the grid no longer holds (its row left the Window, or its column went) is a new `CellNotHeld`.
  An edge moves nothing and tells nothing. After a press on a column's header, an arrow writes
  nothing and tells a new `FromColumnHeader` ("Press a cell to point by keys"); Shift and Ctrl with one
  are told as after a cell (decided with the user on 2026-10-01, until Excel is observed; asked while
  this ticket was built, since ADR-0058 did not say which cell an arrow reaches from a header).
- **The DemoHost.** `/pointing`, a new page in the index: a Sheet and a grid of 40 positions in one
  Scope, the table declaring `Id` and `PV`, and the grid showing Book as its own column between them.
  `/sheet` and `/sheets` have no column the table lacks and paint all their rows, and other specs
  depend on their data.
- **Merged** `claude/exsheet-pointing-scope` at a12352f (ticket 44). Both tickets gave the gate a mode:
  `completionOverPoint` and `pointed` stand side by side in `claimedWhile`, and `GateMode()` picks the
  list's first, since a list never stands over Point written from outside.
- **Tests.** Layer 2: `PointedStepTests` (9: the step up, down and after a reorder, the edges, a row
  not arrived, the columns passed over, a row beyond the painted ones, nothing held, the grid that
  answers, the reveal, a page turned), `PointWrittenFromOutsideTests` (+3: the arrows handed on with
  their modifiers and the gate told `pointed`; a Consumer's rewrite replacing; the grid's own Point
  not handing on), `ShippedStylesheetTests` (+1, and the `claimedWhile` pin), and
  `PointingScopeKeyboardTests` (12, in `PointingScopeTests`; `ScopedSheets` can reorder its columns).
  Layers 1–2 after the merge: 826 + 2072 + 88 + 1148 (1 skipped, as before) + 361, all passing.
  Layer 3, headless on macOS, chrome, on a private port: `pointing-scope.spec.mjs` (+6 on `/pointing`)
  21 passed and 1 skipped (Server-only) on WebAssembly, 22 passed on Server; the first five `/pointing`
  tests 5 times each on Server, 25 passed; `declarations.spec.mjs --grep "SH-36|DC-19|DC-28|DC-31"`
  and `navigation.spec.mjs`, 25 passed on each host.
