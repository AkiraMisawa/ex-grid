# 12: The one undo stack

Status: ready-for-agent

**What to build:** ExSheet holds one undo stack. Each user operation is one step: an edit now, and paste, fill,
insertion and deletion as they arrive. Ctrl+Z undoes and Ctrl+Y redoes. A change the Consumer makes
through ExSheet's commands lands on the same stack. Replacing the Sheet Document clears it.

**Blocked by:** 02

- [ ] Ctrl+Z and Ctrl+Y step through edits in order (ADR-0048)
- [ ] A Consumer command is undone in its place in the order
- [ ] Replacing the document clears the stack
- [ ] Two ExSheets on one page keep separate stacks (ADR-0018)

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
