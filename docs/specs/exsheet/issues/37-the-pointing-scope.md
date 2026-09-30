# 37: The Pointing Scope

Status: done

**What to build:** ExSheet's half of ADR-0058: "The Pointing Scope", "What is written" and "The
keyboard". The Scope lives in the ExSheet package. It registers Sheets and grids, declares a grid
pointed at while a Sheet of its Scope points (ticket 34's declaration), and turns a press that a grid
hands over into the text the Sheet writes, or into a reason.

**Blocked by:** Tickets 34 (the grid's declaration) and 36 (the key)

The Scope:

- [x] A Pointing Scope object the Consumer makes, and passes to each ExSheet and to each registered
      ExGrid. A grid is registered with the Linked Table it shows and its column correspondence (a
      grid column with the same name as its table column needs no entry). The key comes from the
      declaration of the Sheet that points (SH-32)
- [x] A registered grid is pointed at while a Sheet of the Scope holds the keyboard, has an edit
      open and is in Point, and while the grid holds no open edit of its own. Another Sheet is never
      pointed at (SH-32, SH-35)
- [x] When the keyboard leaves the Sheet that points, no grid is pointed at, and the edit stands
      (SH-35, ED-26)

What is written:

- [x] A cell: `XLOOKUP(<key>, T[<key column>], T[<column>])`, with the key written as Excel writes a
      constant of its kind (text quoted with `"` doubled; a number in the invariant form;
      `TRUE`/`FALSE`). A column header: `T[<column>]`. Always the table's column names (SH-32)
- [x] Written where Point writes; a further press, on a registered grid or on the Sheet, replaces
      what this Point wrote (SH-32)
- [x] More than one cell, several columns, a Header Group's rectangle, a column the table does not
      have, a table declared without a key, or a blank key: nothing is written, and ExSheet tells its
      Consumer the reason through a new notification (SH-32)
- [x] A drag: the grid hands over the press as one cell, then once more as several cells or columns
      when the drag reaches another. The text written at the press is taken back, to what it was
      before the press, the dashes go, and the reason is told (ADR-0058, decided 2026-09-30) (SH-32)

The keyboard:

- [x] The first press on a registered grid points (the ninth Windows run: Excel needs two clicks
      for another workbook; this is a deliberate difference) (SH-35)
- [x] After a press on a registered grid, F4 changes nothing, and the Name Box is empty. The arrow
      keys are ticket 41's (SH-35)

The DemoHost:

- [x] `/sheet` registers its positions grid in a Scope. `/sheets` gives each side its own Scope. Each
      shows the notification's reason in a status line. The pages keep their hand-written
      `OutlinedColumns` wiring until ticket 38 moves it into the Scope (SH-32)

Tests:

- [x] Layer 2, one per row of ADR-0058's table, and a press on a grid in no Scope; a registered grid
      with its own open edit; the keyboard leaving and coming back (SH-32, SH-35)
- [x] Layer 3 on `/sheet` and `/sheets`, both hosts: `=`, a press on a PV cell, `*2`, Enter shows
      that row's PV doubled; `=SUM(`, a press on the PV header, `)`, Enter shows the column's sum; on
      `/sheets`, a press on the right's grid while the left Sheet points is an ordinary press. On the Server host with 150 ms injected, a press within the
      round trip after `=` is an ordinary press, and the edit stands (SH-32, SH-35)

## Comments

2026-09-30, implemented on `agent/pointing-scope-37`.

- **The Scope.** `ExSheet.PointingScope`, a class the Consumer makes once per group, as the page is
  made. A Sheet joins it through a new ExSheet parameter, `PointingScope`, and leaves it when given
  another or disposed. A grid is registered with
  `GridPointedAt<TRow> RegisterGrid<TRow>(string table, Func<TRow, IReadOnlyList<Value?>> tableRow, IReadOnlyDictionary<string, string>? tableColumns = null)`,
  and the declaration it returns is passed to the grid's `PointedAt`. `tableColumns` is the
  correspondence (grid column name to table column name; a column named as the table's needs no
  entry). `tableRow` is how a grid row is the table's row, one Value per declared column: the Values
  the Consumer already pushes, so the key written is the one the table holds, whether or not the grid
  shows the key column. The key column and the column names are read from the pointing Sheet's
  declaration (`LinkedTables`), matched without regard to case and written as declared.
- **The reason.** A new ExSheet parameter, `EventCallback<PointingRefusal> OnPointingRefused`, with
  `PointingRefusal(PointingRefusalReason Reason, string Message)`. The reasons are ADR-0058's table
  (`SeveralCells`, `SeveralColumns`, `HeaderGroup`, `ColumnNotInTable`, `NoKey`, `BlankKey`), the
  Placeholder (`RowNotArrived`), and three that the table implies without naming: `TableNotDeclared`
  (the pointing Sheet declares no table of the registered name), `KeyIsAnError` (a key that is an Error
  Value, which no lookup finds) and `NotPointing` (a press handed over by a grid still painted pointed
  at, after the Sheet stopped pointing; told to the Sheet that pointed last). A drag is told as
  `SeveralCells` or `SeveralColumns`, its message saying that what the press wrote was taken back.
  ExSheet raises it and shows nothing itself; `/sheet` and `/sheets` show it below their grids
  (`#sheet-pointing`, `#sheets-pointing`), so no row of the page's tests moves.
- **What is written** comes from the engine: `FormulaEntry.LookupText`, `StructuredReferenceText`
  (the engine's own escaping of `[ ] # '`) and `ConstantText` (text quoted with `"` doubled, a number
  as the engine writes a number constant, `TRUE`/`FALSE`, and null for an Error Value).
- **ExGrid's mechanism**, generic: `Task<bool> WritePointedTextAsync(string text)` writes text handed in
  from outside where Point writes (in place of what this Point wrote, from the grid's own cells or from
  outside, or else at the caret where `PointAt` says a Reference can go), and
  `Task<bool> TakeBackPointedTextAsync()` returns the edit to what it was before the last such write
  (text, caret, editing state, and a Reference the grid had pointed at, with its outline). While text
  written from outside stands, the Name Box is empty, F4 changes nothing, and the arrows, Home and End
  are claimed and move and write nothing (ticket 41 gives them their meaning); a press on the grid's
  own cell replaces it. `[Parameter] EventCallback<PointState> OnPointStateChanged` tells where the
  open edit stands (`None`, `InPoint`, `WrittenFromOutside`) at each change, before the render that
  paints it; a caret not yet reported is waited for rather than told as out of Point, so the Scope
  does not stop and start with every key. `GridPointedPress` gains `Dragged`, so the Consumer can tell
  a drag's second hand-over from a Shift+press. And a grid with an open edit of its own is not pointed
  at whatever its declaration says: ticket 34 had stated that as the Consumer's to keep and not checked
  it. The grid knows its edit at once, where a Scope would have learned it a render late, and only
  through the Consumer wiring each grid's `OnEditingChanged` a second time.
- **Holding the keyboard.** The grid tells its Point state in C#. Where DOM focus is, ExSheet hears from
  Blazor's `focusin`/`focusout` on one element around its markup (`div.ex-sheet`, `display: contents`,
  so nothing is laid out differently), listened to only while a Scope is given. The browser raises the
  `focusout` of a move inside the Sheet (the root to the Cell Editor) before its `focusin`, in one task,
  so the Sheet waits one turn of the renderer's queue before it says the keyboard left; a `focusin` in
  between is the keyboard moving inside. No JavaScript was added.
- **Dashes** were not drawn: the Scope would also have to hear when an operator is typed after the
  press to take them away, and `OnPointStateChanged`'s `WrittenFromOutside` is there for ticket 38 to
  do that. So a drag's "the dashes go" has nothing to take away yet.
- **The pages** keep their hand-written `OutlinedColumns`. `edit-stands.spec.mjs`'s `/sheet` tests press
  the positions grid after the keyboard has left the Sheet for the page's Revalue button: while the
  Sheet points, that press now points, which is the point of this ticket.
- **Tests.** Layer 1, `PointedTextTests` (13): the constants of each kind, the structured reference,
  the lookup, and each read back as the cell it was written for. Layer 2, `PointingScopeTests` (25):
  each row of ADR-0058's table through the grid's own presses (Shift, a drag, a drag across headers, a
  Header Group, a column the table does not have, no key, a blank key), the Placeholder, an Error
  key, an undeclared table, the correspondence (`Value` is the table's `PV`), a key with a quote, a
  further press replacing, F4 and the Name Box, a grid in no Scope, a registered grid with its own open
  edit, the keyboard leaving and coming back, the keyboard moving inside the Sheet, only the Sheet with
  the keyboard pointing, a press after pointing ended, a Sheet leaving; `PointWrittenFromOutsideTests`
  (12) for ExGrid's mechanism, and one more in `PointedAtTests`. Layer 3, `pointing-scope.spec.mjs`
  (9), on both hosts, headless on macOS: the four cases above and a further press, F4 and the Name Box,
  a Shift+press and a drag with the status line, and the keyboard leaving for a page button.
- **Found, for ticket 35.** On the Server host at 0 ms injected, `=1+` typed and the positions grid
  pressed at once lost the `1+` in 2 runs of 4 (`=XLOOKUP(...)` where `=1+XLOOKUP(...)` was typed): the
  `1` and `+` were still held behind `=` by the Sheet's listener when the press, which the positions
  grid sends itself, reached the core. DC-54 speaks of the keys typed after a press handed on; the keys
  typed before it, still held, need their place kept too. `pointing-scope.spec.mjs` waits for the
  typed text before it presses, and leaves that case to ticket 35.
