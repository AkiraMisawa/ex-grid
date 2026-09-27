# 12: The one undo stack

Status: done

**What to build:** ExSheet holds one undo stack. Each user operation is one step: an edit now, and paste, fill,
insertion and deletion as they arrive. Ctrl+Z undoes and Ctrl+Y redoes. A change the Consumer makes
through ExSheet's commands lands on the same stack. Replacing the Sheet Document clears it.

**Blocked by:** 02

- [x] Ctrl+Z and Ctrl+Y step through edits in order (ADR-0048)
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

2026-09-27, ExGrid core half of the route (ADR-0050 item 8, DC-30): `ExGrid.OnUndo` and
`ExGrid.OnRedo`, two `EventCallback`s, each a declaration. Declared, the grid hands the key gate
`GridKeys.TakenFor(undo, redo)`, which adds `Control+z`/`Control+Z` for undo and
`Control+y`/`Control+Y`/`Control+Shift+Z`/`Control+Shift+z` for redo (both cases for CapsLock
and Command+Shift) to `GridKeys.Taken`; `Control` is the Primary Modifier, so Command+Z on an
Apple keyboard is the same key. The gate consults that set only while no edit is open, so while
one is, Ctrl+Z stays the Cell Editor's or the Formula Bar's own undo of uncommitted typing
(ADR-0007), and in a Consumer's control or the Name Box it stays the control's. The forwarded
key resolves to `GridKeyKind.Undo`/`Redo` and raises the callback; it renders no row. Undeclared,
`GridKeys.Taken` is handed unchanged and the keys stay the browser's (DC-1). A declaration made
or withdrawn after attach re-tells the gate through a new `setTaken(keys)` on the module's
per-instance handle (the one JavaScript change: the set is C#'s list, as at attach). Layer 1:
`GridKeyTests`; layer 2: `UndoRedoKeyTests`, and the script inspection in
`ShippedStylesheetTests`. **What remains:** ExSheet passes `UndoAsync`/`RedoAsync` as `OnUndo`/
`OnRedo` (the first criterion), and layer 3 presses the real keys: raised with no edit open,
the editor's own undo with one open, and the browser's without the declaration (DC-30).

2026-09-27, the keys: ExSheet declares `OnUndo` and `OnRedo` on its ExGrid, two `EventCallback`s
made once per instance and held in fields, which call `UndoAsync` and `RedoAsync` — the stack a
Consumer walks. Ctrl+Z undoes and Ctrl+Y / Ctrl+Shift+Z redo while no edit is open; while one
is, the keys stay the editor's own (ADR-0007). Layer 2:
`UndoStackTests.Ctrl_Z_and_Ctrl_Y_step_through_edits_in_order` and
`While_an_edit_is_open_Ctrl_Z_leaves_the_stack_alone`. Every criterion here is met. DC-30's
layer 3 half (the real keys in a browser, with and without an edit open) is ticket 18's.
