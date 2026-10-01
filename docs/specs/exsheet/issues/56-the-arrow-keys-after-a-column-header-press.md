# 56: The arrow keys after a column header press

Status: done

**What to build:** ADR-0058, "What Part B of the ninth Windows run settled", the fourth bullet (Q52),
which replaces Q48's refusal; and the bullet after it, on a drag that took back what its press
wrote. Excel, pointing at a whole column of another workbook, went with ↓ to the column's first row of
data and with → to the next column (`verification/2026-10-01-windows-9/pointing-scope.md`, cases y1
and y2).

**Blocked by:** None. It shares nothing with ticket 55 but the Sheet's test files; keep to the
Pointing Scope's tests.

- [x] After a press on a registered grid's column header, ↓ points at the column's first row in the
      grid's current order: the `XLOOKUP(...)` of that row's key is written, replacing `T[<column>]`,
      and the dashes move to its cell. On `/pointing`, `=`, a press on PV's header, ↓ gives
      `=XLOOKUP("R-1", Positions[Id], Positions[PV])`. A first row that has not arrived, or whose key
      is blank or an Error Value, is refused as an arrow reaching it is. The grid scrolls the cell into
      view (SH-35)
- [x] After a press on a column header, ← and → point at the next column the table has, as a
      column: `T[<that column>]` is written and its body dashed, passing over the grid's columns the
      table does not have. On `/pointing`, ← from PV's header gives `=Positions[Id]`, passing over
      Book; with no such column that way, nothing moves (SH-35)
- [x] ↑ after a header press is an edge: nothing moves and nothing is told. Shift+arrow and
      Ctrl+arrow are refused as from a cell (SH-35)
- [x] The grid answers a step from a column (DC-55): down with the column's first row, left or right
      with the nearest named column, as a column; up is an edge. Extend `GridPointedAt.StepAsync` (or
      add its column form) without JavaScript; every public member keeps its XML doc comment
- [x] `PointingRefusalReason.FromColumnHeader` and its words go, unless something still tells it;
      say which in the comments
- [x] After a drag took back what its press wrote, the arrows are the Sheet's own Point (`=` ↓ points
      at the cell below the edited one), as Part B read: pin it in Layer 2, so it cannot change
      unnoticed (SH-35)
- [x] Layer 2 in ExGrid for the column step and in ExSheet for the Scope; Layer 3 in
      `pointing-scope.spec.mjs` on `/pointing`: a header press then ↓, and a header press then ←
      (SH-35)

## Comments

2026-10-01, implemented on `agent/ps-56`.

- **ExGrid (DC-55).** `GridPointedAt.StepFromColumnAsync(column, direction, isColumn)` is the column
  form of `StepAsync`. Down answers the column's first row in the grid's current order as a `Cell`.
  That row is position 0 of the whole result, so under a `PageSize` it is on the first page. A first
  row that is a Placeholder answers `RowNotArrived`, and a grid with no rows answers `Edge`. Left and
  right answer the nearest column `isColumn` accepts as a new kind, `GridPointedStepKind.Column`,
  which names the column and no row. Up is `Edge`. `NotHeld` means the grid shows no column of that
  name, is not pointed at, or is given no declaration. As with the cell step, the step moves no
  Selection and no scroll. The cell step and the column step now share one scan for the nearest
  column. No listener, interop call or script is added.
- **ExSheet (SH-35).** `PointingScope.PointArrowAsync` now steps from the dashed column instead of
  refusing. A cell reached is written as an arrow reaching it writes it, replacing `T[<column>]`, and
  the grid reveals it. A column reached is written as a press on its header writes it (`T[<column>]`,
  its body dashed; the write is shared with the press). Nothing is revealed for a column: DC-55 asks
  only that a cell be scrolled into view. The column step is a gesture of its own, as the cell step
  is: a drag still held from the header press takes nothing back. Shift and Ctrl with an arrow are
  told before the step is asked, so they are refused as from a cell. A first row whose key is blank
  or an Error Value, or that has not arrived, goes through the same lookup as a press, and is refused
  with the same reason. When the dashed column is no longer shown, `CellNotHeld` is told, and its
  words name the column ("…no longer shows the column 'Id' of 'Positions' pointed at…").
- **`PointingRefusalReason.FromColumnHeader` went**, with its words in `SheetWords`: after this
  ticket nothing tells it. The verification records of the ninth run still hold its old message, as
  what was observed then.
- **After a drag took back its press**, the Scope had already done what Part B read: the take-back
  restores the dashes that stood before the press, none, so the arrows reach the Sheet's own Point.
  Two Layer 2 tests now pin that, one for a drag over cells and one for a drag across headers.
- **Docs and the DemoHost.** The Scope's and the Sheet's doc comments, `src/ExSheet/README.md`, and
  `/pointing`'s prose say what the arrows do after a header press.
- **Not built, noted for a later decision:** a column that ← or → reaches is not scrolled into view
  horizontally. DC-55 says "scrolls a cell into view", and ADR-0058 speaks of "the pointed cell".
  `/pointing` shows all its columns, so nothing on it is hidden. In a grid narrower than its columns,
  the column reached could be dashed outside the view.
- **Tests.** Layer 2: `PointedStepTests` +5 (down to the first row in the current order with the grid
  scrolled away from it; a first row not arrived and a grid with no rows; up an edge; left and right
  as a column, passing over the unnamed; nothing held). `PointingScopeKeyboardTests`: the header
  refusal test is replaced by 9 tests. They cover ↓ to the first row; the current order and the
  scroll back to it; ← and → as a column, passing over Note; ↑ an edge with Shift and Ctrl refused; a
  blank or not-yet-arrived first row; a column no longer shown; a held header drag after an arrow;
  and the arrows after a drag over cells or across headers took back its press. `ScopedSheets` can
  show its rows from a `WindowStart`. Layers 1–2: 826 + 2072 + 88 + 1153 (1 skipped, as before) +
  369, all passing. Layer 3 ran headless on macOS, chrome, on private ports. In
  `pointing-scope.spec.mjs` the `/pointing` header test is replaced by two tests: `=`, a press on
  PV's header, then ↓, gives R-1's lookup and scrolls the grid back to R-1 from R-10; and
  `=COUNTA(`, a press on PV's header, then ←, gives `Positions[Id]` over Book, dashed down the
  column, with ↑ and a further ← moving nothing, and `)` and Enter giving 40. WebAssembly: 22 passed
  and 1 skipped (the Server-only test). Server: 23 passed.
