# 23: F4 cycles the Reference at the caret

Status: ready-for-agent

**What to build:** ADR-0051, "F4 cycles the Reference at the caret" (2026-09-29), with ADR-0021's
note of the same day. While an edit is open, F4 cycles the Reference at the caret: `=B2`, F4, gives
`=$B$2`, then `=B$2`, `=$B2`, `=B2`. The core claims the key and asks the Consumer; ExSheet rewrites
the text from its parser.

**Blocked by:** None (can start immediately)

Core (ExGrid):

- [x] A Consumer declaration: a synchronous function over the editor's text and its selection
      (start, end) that answers the new text and the new selection, or nothing. Off by default; a
      plain ExGrid is unchanged (DC-1, DC-45)
- [x] Declared, the capture-phase listener claims F4 only while an edit is open, in the Cell Editor
      or the Formula Bar, and sends the text and selection with the key message; with no edit open,
      F4 is not claimed (DC-45)
- [x] The answer is written to the editor surface the key came from, and to the other surface as any
      edit is, and its selection is placed by the listener that already places the caret (DC-45)
- [x] While pointing, F4 rewrites the Reference the outline wrote, and pointing goes on; a further
      move writes the next Reference as pointing writes it (ADR-0051 reading) (DC-45)
- [ ] A burst of F4 presses on the Server host with 150 ms injected gives the four forms in order:
      each press is decided from the text its own key message carries (DC-45)
- [x] No JavaScript use is added; the listener stays inside ADR-0021's keyboard/editor use (DC-24)

ExSheet:

- [x] The rewrite, pure and tested in layer 1 against ADR-0051's readings: the Reference the caret is
      inside or touching; a range as one, from its first end; every Reference in a selection; whole
      columns and rows in two forms; the Sheet qualifier kept; structured references, function
      names, numbers and non-Formula text unchanged; the caret at the end of the rewritten Reference,
      or the selection over the rewritten span (SH-28)
- [x] ExSheet declares the function, so `/sheet` in the DemoHost cycles on F4 (SH-28)
- [ ] Layer 3: `=B2` and four F4 presses in a cell and in the Formula Bar, on both hosts (DC-45)

## Comments

The readings are asked of Excel in [verify-on-windows-6.md](../verify-on-windows-6.md), Part A. A
reading Excel contradicts is a defect of the reading, fixed after the run with the ADR paragraph.

2026-09-29, built on `claude/exsheet-f4`. What is left is the layer 3 run, which settles the two
open boxes; the spec is written and has not been run here (the DemoHost's port is shared).

- **Core.** `CycleReference` (`Func<string, int, int, EditorRewrite?>`, `ExGrid.Pointing.cs`) with
  its answer `EditorRewrite` (`src/ExGrid/Cells/EditorRewrite.cs`). `OnKeyAsync` hands F4 to
  `OnCycleReferenceKey` while an edit is open and the function is declared; the answer is written
  to `_editText`, so both surfaces show it, and `PlaceCaret` now carries a selection end, which
  `setCaret` places. While pointing, the function is asked at the end of the outline's Reference
  and pointing goes on over the rewritten span. An answer whose selection is outside its text is
  refused by name. `setEditing` tells the gate a third flag, and the gate adds F4 to the editing
  set only then (`ex-grid.js`); with no edit open F4 is never claimed.
- **The key message** now also carries the selection's end, and whether the user moved the caret
  in that very text (the listener's own `movedByUser` note). The second is for a press made
  before the core's caret placement has landed: setting the value leaves the browser's caret at
  the end, and a second F4 in that gap on a circuit would otherwise cycle the Reference at the end
  of the text (`=A1+B2`, caret after A1, F4 twice → `=$A$1+$B$2` instead of `=A$1+B2`). The core
  uses the placement's selection there, as it already disregards a caret report ahead of a
  placement (ADR-0051's second round), unless the user moved the caret.
- **ExSheet.** `FormulaEntry.CycleReference` in the engine rewrites only the `$` signs, over the
  operands its tolerant scan finds, each checked against the Lexer's own Reference grammar
  (`Lexer.MatchReference`), so `LOG10(`, `Positions[PV]`, `ZZZ1` and `TRUE` are not References.
  `SheetFormulaAids.CycleReference` translates, and `ExSheet.razor` declares it.
- **Tests.** Layer 1: `tests/ExSheet.Engine.Tests/ReferenceCycleTests.cs` (SH-28, every reading).
  Layer 2: `tests/ExGrid.Components/ReferenceCycleTests.cs` (DC-45, DC-1), the gate's shape in
  `ShippedStylesheetTests`, and ExSheet's wiring in `FormulaEntryWiringTests` (SH-28). Layer 3,
  written and not run: the `DC-45:` tests in `tests/ExGrid.Browser/declarations.spec.mjs`.
- **Readings the implementation took where ADR-0051's list is silent**, to be asked of Excel with
  Part A: a selection that overlaps no Reference changes nothing (`=A1|+|B2`, selecting only the
  `+`); a Reference partly inside a selection counts as covered; the text keeps the case and the
  order it was typed in (`=b2` → `=$b$2`, `=B2:A1` → `=$B$2:$A$1`), since only `$` signs are
  written.
