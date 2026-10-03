# 13: Inserting and deleting rows and columns

Status: done

**What to build:** Commands on the Context Menu insert and delete rows and columns. References are rewritten to keep
naming the same cells, and a deleted target is `#REF!`. Because rows and columns are places, the
Row Sequence Version does not move and the Selection stays where it was. Each command is one undo
step.

**Blocked by:** 03, 12

- [x] Inserting above a referenced cell rewrites every Reference to it (ADR-0046/0047)
- [x] Deleting a referenced cell makes the Formula `#REF!`
- [x] The Selection stays in place after an insertion, as in Excel (ADR-0011/0046)
- [x] One Ctrl+Z restores the structure and every Reference

## Comments

2026-09-27, engine half: `Sheet.InsertRows`, `DeleteRows`, `InsertColumns` and `DeleteColumns`
move Entries, number formats and alignment, and rewrite every Reference in every Formula,
relative and absolute alike, to keep naming the same cells: a range moves when the edit is at or
above its first row, grows when it is inside it, shrinks on a partial deletion, and is written
`#REF!` in the stored Formula when every cell it named is deleted (`=SUM(#REF!)`, `=#REF!+1`).
`A:A`, and `A1:A1048576`, which Excel writes as `A:A`, stay put on a row edit, but a Formula
reading them recomputes. Only the rewritten Formulas (and what reads them) recompute. The
returned `SheetChange` names every row whose Value, Entry or formatting at that address
differs, which on an insertion is every non-blank row below it (`StructureTests`). Covers the
engine side of the first two criteria and, with ticket 12's `SheetStep`, of the fourth. An
insertion that would push an Entry off the Sheet is refused by name, as Excel refuses it; one
that would push the cells a Reference names off the edge is refused too, because what Excel
writes there is not pinned (reported for a decision). A cell holding only formatting is
dropped at the edge. **What remains is the component's:** the Context Menu commands, keeping
the Row Sequence Version and the Selection in place (SH-5's layer 2 half), and handing the
change's rows back as new row instances. Not implemented, reported for a decision: Excel gives
an inserted row the formatting of the row above (and a column that of the column to its left);
the engine inserts blank rows.

2026-09-27, ExSheet wiring: the grid's Context Menu (`ContextCommands`, ADR-0036) carries four
ExSheet commands: insert rows above, delete rows, insert columns to the left, and delete columns.
Each acts on the rows or columns the Selection spans (two rows selected insert two) and does its
`SheetEdit` through `PerformAsync`, so it is one step on the undo stack. The ids are public
(`SheetCommandIds`) and the labels go through a new `ExSheet.CommandLabel`: the Consumer's
wording first, then ExSheet's English, then the grid's built-in words for the grid's own commands.
A Selection of several ranges has no one span, so the commands are unavailable for it rather
than acting on a guess. A refusal (`EntriesWouldLeaveSheet`, …) changes nothing, puts the
engine's sentence in the notice, and records no step. The Row Sequence Version stays 0 and the
grid is never told of a new Selection. Only the rows the change names get new instances. Layer 2:
`StructureCommandTests` (the menu and its words, the rewritten References including a range
growing, two rows, `#REF!` on a deleted column, one undo restoring structure and References, render
counts, a refusal). **Still open:** the fourth criterion's Ctrl+Z. `UndoAsync` restores the
structure and every Reference, but the key waits on the core's undo route (ADR-0050, item 8;
ticket 12).

2026-09-27, layer 3 (ticket 18), run locally under xvfb with Playwright's Chromium (build 1194; this machine has neither Google Chrome nor Edge, so the committed config's `chrome` and `msedge` projects are CI's to run), against the WebAssembly host and the Server host behind the latency proxy. `sheet.spec.mjs`: a row inserted
by the page's button and one by the Context Menu's "Insert rows above" keep `=SUM(B2:B4)` and
`=B2*C2` naming their cells (`=SUM(B4:B6)` after both), and one Ctrl+Z per insertion restores the
structure and every Reference. Only row insertion is driven in the browser; deletion and columns
are not, so the last criterion stays open here.

2026-09-27, layer 3, the rest: `sheet.spec.mjs` now drives the other three commands from the
Context Menu on `/sheet`. Deleting row 3 moves the total up and shrinks it (`=SUM(B2:B3)`, 32),
turns a Formula naming the deleted row into `=#REF!`, and leaves the Selection on B3; inserting a
column left of C (Selection C2:C3) keeps Amount multiplying Qty by Price (`=B2*D2`,
`=SUM(E2:E4)`) and leaves the Selection over C2:C3; deleting column C writes `=B2*#REF!` and
leaves the Selection on C2. After each command the grid has the keyboard, one Ctrl+Z restores the
structure and every Reference (read back in the Formula Bar), and Ctrl+Y redoes it. Run locally
under xvfb with Playwright's Chromium (build 1194) against the WebAssembly host and the Server
host behind the latency proxy: `sheet.spec.mjs` 13/13 on each. The committed config's `chrome`
and `msedge` projects are CI's to run. All four criteria are met.

2026-09-30, a note for readers of the first comment: "the engine inserts blank rows" is no longer
so. ADR-0046 settled that an inserted row takes the formatting of the row above, and a column that
of the column to its left, and the engine copies it (`FormatInserted`, `ShiftAxisStyles`). ADR-0071
extends the same rule to Font, Fill and Border.

