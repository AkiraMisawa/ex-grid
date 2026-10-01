# 56: ExSheet's commands act on the grid's Selection as it is now

Status: done

**What to build:** the rest of what ticket 51's Server fix found. It is in ADR-0050 item 14's note
of 2026-10-01 and in ticket 51's comments. The grid raises `SelectionChanged` after the render that
shows a move, which on a circuit is a round trip later. A declared key now carries the grid's
Selection. Three other paths still act on the Selection ExSheet last heard. A command run within a
round trip of a keyboard move then formats fewer cells, or other cells, than are selected, and
nothing says so. That is quietly wrong (principle 1).

**Blocked by:** None (can start immediately)

- [x] **The Consumer's commands** act on the grid's current Selection, not ExSheet's copy:
      `SetCellFormatAsync` and its shorthands, and `OpenFormatCellsAsync`.
  - This needs a way to read that Selection from the grid: an opt-in read of the core's, with the
    Row Sequence Version, as ADR-0011 asks of positions.
  - A toolbar button pressed straight after Shift+arrow, on the Server host, formats the extended
    range.
- [x] **The Context Menu's "Format Cells…"** opens over the Selection the menu was opened on. Its
      context carries the ranges but not the Focus. Either it gains the Focus, or the command reads
      the grid's Selection as above.
- [x] **A whole-column resize** groups its undo step by the grid's Selection. Today a stale one at
      worst splits the step, and no data goes wrong.
- [x] **Tests**:
  - Layer 2 stages the circuit's order, as ticket 51's fix does: a Range Request left unanswered
    holds the selection notification back.
  - Layer 3 on the Server host: a toolbar button straight after Shift+ArrowDown.


## Comments

*(2026-10-01, agent cf-56, built.)* ExSheet's commands act on the grid's Selection as it is now.

- **The core: a read of the Held Selection.** `public HeldSelection ExGrid<TRow>.ReadSelection()`
  answers the grid's Selection with the Row Sequence Version it is written in, as the grid holds
  it. `HeldSelection` is the existing public `readonly record struct HeldSelection(int
  RowSequenceVersion, GridSelection Selection)`; its `Under(int)` drops the positions under any
  other version (ADR-0011). It is a read, synchronous, on the renderer's context, beside
  `PlaceSelectionAsync`. A Consumer that never calls it sees no change. Nothing reaches JavaScript.
- **ExSheet** adopts `ReadSelection().Under(0).Selection` as the Selection it acts on, in:
  - `SetCellFormatAsync`, and so `SetNumberFormatAsync` and `SetAlignmentAsync`;
  - `OpenFormatCellsAsync`;
  - the Context Menu's "Format Cells…". The menu's context still carries no Focus. The grid's
    Selection is the menu's while the menu stands, since a move closes it;
  - a whole-column resize, at the column that starts the undo step.
  Adopted, the late `SelectionChanged` names the Selection already held, so Format Cells, opened
  over it, stands.
- **A formatting key still acts on the Selection it carries.** `OnFormatKeyAsync` now calls
  `FormatSelectionAsync` rather than `SetCellFormatAsync`, which would read the grid again. For
  the merge with ticket 58: anything the keys, OK and the commands must all do belongs in
  `FormatSelectionAsync`, the path the three share.
- **Layer 2** stages the circuit's order.
  - The core (`SelectionReadTests`): a Range Request left unanswered holds the notification back,
    and the read answers Ctrl+Shift+Down's range before the Consumer hears it. A new order answers
    nothing selected under the new version.
  - ExSheet (`CommandSelectionTests`) cannot leave its own Range Request unanswered, so it uses two
    other orders:
    - A page's own button (`Support/SheetWithCommand.razor`) whose handler forwards Shift+ArrowDown,
      or Ctrl+Space, to the grid and gives the command. A handler's synchronous part runs inside
      its event's batch, so the grid's render and the notification after it come later. Each test
      asserts the page had not heard the move when the command ran. This stages the three
      commands, `OpenFormatCellsAsync` with Format Cells standing when the notification lands, and
      a B:D resize as one step.
    - The menu's focus held through bUnit's JS interop. The notification waits behind it, as it
      waits behind that round trip on a circuit.
  - With `AdoptGridSelection` made a no-op, all six fail and the other 414 pass.
- **Layer 3**, `format-keys.spec.mjs` and `format-cells.spec.mjs`, on Chrome, headless on macOS:
  17 of 17 on each host.
  - New, at 150 ms on the Server host:
    - `#sheet-money` straight after Shift+ArrowDown formats B2:B3 as one step;
    - `#sheet-format-cells` straight after Shift+ArrowDown opens over C2:C3 and stands;
    - the Context Menu's item chosen on C3 with C2 selected opens over C3.
  - With the fix disabled, the two button tests fail: B3 stayed `7`, and the late notification
    ended Format Cells.
- **Found, not resolved: the menu test does not catch the race on a real circuit.** With the fix
  disabled it passed at 150 ms and at 600 ms. By the time the click on the item reached ExSheet,
  the move had been heard. Why was not traced. It may be that the click waits for the menu to stand
  still, or that some render in between flushes the held notification. So the menu's order is
  covered in layer 2 only. The layer-3 test pins the outcome and says so in its comment.
- Layers 1 and 2: `ExGrid.Tests` 857, `ExSheet.Engine.Tests` 2107, `ExGrid.MudBlazor.Tests` 88,
  `ExGrid.Components` 1097 and 1 skipped (3 new), `ExSheet.Components.Tests` 420 (6 new).
