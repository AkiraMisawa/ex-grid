# 12: The one undo stack

Status: needs-info

**What to build:** ExSheet holds one undo stack. Each user operation is one step: an edit now, and paste, fill,
insertion and deletion as they arrive. Ctrl+Z undoes and Ctrl+Y redoes. A change the Consumer makes
through ExSheet's commands lands on the same stack. Replacing the Sheet Document clears it.

**Blocked by:** 02

- [ ] Ctrl+Z and Ctrl+Y step through edits in order (ADR-0048)
- [x] A Consumer command is undone in its place in the order
- [x] Replacing the document clears the stack
- [x] Two ExSheets on one page keep separate stacks (ADR-0018)

## Comments

2026-09-27, engine half: every user operation is described as a `SheetEdit` (typed entry, a
batch of typed entries such as a paste from another program, Entries set directly, number
format, alignment, insertion and deletion of rows and columns; paste of Entries and fill join
with tickets 14 and 15), and `Sheet.Do(edit)` does it and returns one `SheetStep`, whose
`Undo()` and `Redo()` each report a `SheetChange` from one completed recalculation. An edit in
place is undone by putting back what the touched cells recorded; an insertion or deletion by
the inverse edit, then the Formulas it rewrote and the cells it dropped put back exactly, so a
`#REF!` and a shrunken range return as they were (`UndoStepTests`: undo after do and redo after
undo are the identity on the Sheet Document and every Value, for each kind of operation and for
a stack of them). `Sheet.Check(edit)` says whether an edit would be refused, without doing it; a
refused or unreadable edit is no step. **What remains is the component's:** the stack itself,
Ctrl+Z and Ctrl+Y, putting Consumer commands on the same stack as `SheetEdit`s, clearing it
when the document is replaced, and two ExSheets keeping two stacks (all four criteria are layer
2).

2026-09-27, component half: ExSheet holds one `SheetHistory` per instance. Every user edit and
every Consumer command (`ExSheet.DoAsync(SheetEdit)`, and the formatting commands
`SetNumberFormatAsync` / `SetAlignmentAsync`) is one `SheetStep` on it, in the order done;
`UndoAsync` and `RedoAsync` walk it, repaint only the rows each step's `SheetChange` names, and
raise the Sheet Document; a new operation forgets what was undone; a refused operation is no
step; handing in a different document clears it, and handing back the one ExSheet raised does not.
Layer 2: `UndoStackTests` (order, render counts, a Consumer command in its place, a refused
command, the document raised, replacement, two instances).

**Blocked: Ctrl+Z and Ctrl+Y do not reach ExSheet.** ADR-0007 says "the grid only forwards
Ctrl+Z", but no such route exists in the core: `GridKeys` claims neither `Control+z` nor
`Control+y`, so the capture-phase listener never sends them to `OnKeyAsync`, and ExGrid has no
parameter through which a Consumer hears an unclaimed key. A bubble-phase `@onkeydown` on an
element around the grid is not a substitute: it cannot tell a Ctrl+Z typed into the Cell Editor or
the Formula Bar (the editor's own undo of uncommitted typing, ADR-0007's last line) from one on the
grid, because ExGrid does not expose whether an edit is open, and it would sit outside the root
the key listener belongs to (ADR-0018). The proposal returned to the orchestrator: an opt-in core
declaration in ADR-0050's pattern — the core claims Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z while no edit
is open (the editor keeps its own undo) and raises an `OnHistoryKey`-style callback with undo or
redo; without a delegate the keys stay the browser's, as today. With it, the first criterion is a
few lines in ExSheet.
