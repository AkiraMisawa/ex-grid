# ExSheet holds a Workbook of Sheets

Status: ready-for-agent

Decided in [ADR-0331](../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) (W1 to W9,
2026-10-20). This spec gathers it into work; it decides nothing the ADR does not. `.xlsx`
([ADR-0330](../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md)) waits
on it and has a spec of its own.

## Problem Statement

An ExSheet holds one Sheet. A user who keeps a budget across months, or inputs on one Sheet and a
summary on another, has to put everything on one grid, or use two ExSheets that cannot refer to each
other. Most workbooks people bring from Excel have several Sheets, so `.xlsx` cannot be opened whole.

## Solution

An ExSheet shows a **Workbook**: Sheets in order, with a **Sheet Tab** for each at its foot, as Excel
does. Formulas reach any Sheet (`Sheet2!A1`) and span several (`SUM(Jan:Dec!B2)`). Sheets are added,
renamed, moved, copied and deleted from the tabs or the keyboard, and every one of those is a step on
the one undo stack. The application exchanges a **Workbook Document**; a Sheet Document still opens, as
a Workbook of one Sheet.

## User Stories

1. As a user, I want to see a tab for each Sheet at the foot of the ExSheet, so that I know which Sheets the Workbook holds.
2. As a user, I want to click a tab to show its Sheet, so that I can move between Sheets.
3. As a keyboard user, I want Ctrl+PageDown and Ctrl+PageUp to show the next and previous Sheet, as in Excel.
4. As a user, I want each Sheet to keep its own selection, active cell, scroll position and column widths, so that coming back to a Sheet finds it as I left it.
5. As a user, I want to add a Sheet with the `+` button or Shift+F11, so that I can start a new one.
6. As a user, I want a new Sheet to take the next free name (`Sheet2`, `Sheet3`, …), as Excel does.
7. As a user, I want to rename a Sheet by double-clicking its tab and typing, so that its name says what it holds.
8. As a user, I want a name Excel would refuse (too long, a forbidden character, empty, already taken) refused by name, so that I know why.
9. As a user, I want Formulas that name a renamed Sheet to follow the new name, so that nothing breaks.
10. As a user, I want to delete a Sheet from its tab's menu, so that I can remove what I no longer need.
11. As a user, I want a Formula that read a deleted Sheet to show `#REF!`, as Excel does.
12. As a user, I want Ctrl+Z to bring a deleted Sheet back with its Formulas and the Formulas that read it as they were, so that a deletion is never a loss.
13. As a user, I want to move a Sheet by dragging its tab, or with Move left / Move right on its menu, so that I can order them.
14. As a user, I want to copy a Sheet, getting `Name (2)` as Excel does, so that I can start from an existing one.
15. As a keyboard-only user, I want every tab operation reachable without a pointer, so that nothing needs a mouse.
16. As a screen-reader user, I want the tabs read as a tab list with the shown Sheet selected, and each operation announced.
17. As a user, I want a Formula `=Sheet2!A1` to show Sheet2's A1 and to change when it changes.
18. As a user, I want inserting or deleting rows and columns on one Sheet to rewrite the References to it on every other Sheet, as Excel does.
19. As a user, I want `=SUM(Sheet1:Sheet3!B2)` to add B2 on every Sheet from Sheet1 to Sheet3 in tab order, as Excel does.
20. As a user, I want a 3D Reference given to a function that does not take one to answer as Excel answers.
21. As a user, I want moving, inserting or deleting a Sheet inside or at the end of a 3D Reference's span to change the span as Excel changes it.
22. As a user typing a Formula, I want to press another Sheet's tab and point at a cell there to write `Sheet2!A1`, and Enter to commit on the Sheet I started from and show it again.
23. As a user, I want the Name Box to take `Sheet2!B5` and go there.
24. As a user, I want Ctrl+Z to show the Sheet the undone step was taken on and select the range it changed.
25. As a user, I want Find and Replace to search Within: Sheet (the default) or Workbook, and a match on another Sheet to show that Sheet.
26. As a user, I want copying and pasting between Sheets to shift relative References as Excel does.
27. As a user, I want a key typed at machine speed after a tab press to land on the Sheet the press showed, never on the one before.
28. As an application developer, I want to pass a Workbook Document in and hear it change, so that I persist the whole Workbook.
29. As an application developer, I want my existing Sheet Document code to keep working, so that adding Sheets does not force a rewrite of my page.
30. As an application developer, I want setting both `Workbook` and `Document` refused by name, so that I never wonder which one won.
31. As an application developer, I want commands to name their Sheet, and a command that names none refused once there are several Sheets, so that a command never lands on whichever Sheet the user happened to be looking at.
32. As an application developer, I want commands to add, delete, rename, move and copy Sheets, and an event for each change, so that my own buttons can do what the tabs do.
33. As an application developer, I want my application's Sheet operations on the same undo stack as the user's.
34. As an application developer using the engine directly, I want a `Workbook` that opens a Workbook Document and computes it, with no component.
35. As an application developer, I want Linked Tables declared on the Workbook and readable from every Sheet.
36. As an application developer, I want the Workbook's culture to be one, so that `1,234` is the same number on every Sheet.
37. As a MudBlazor user, I want the Sheet Tabs drawn by the Wrapper in MudBlazor's look, behaving exactly as the Built-in ones.
38. As a Docs Site reader, I want a page and an Example for the Workbook, with the Built-in / MudBlazor switch.

## Implementation Decisions

- **The engine gains `Workbook`** (ADR-0331 §9): it holds the Sheets in order, one dependency graph across them, the Linked Tables, the culture, and applies every edit as one step returning the step that undoes it. `Sheet` becomes one Sheet of a Workbook. The members that move (opening a document, producing one, applying an edit) leave `Sheet`; no forwarders are kept. The removal is a Change Note in `0.2.0`.
- **The Workbook Document** is a format of its own with a version (ADR-0331 §2): the Sheets in order (each recording what a Sheet Document records today, without culture or Linked Tables), the Sheet shown, the culture and the Linked Tables. A Sheet Document opens as a Workbook of one Sheet.
- **Recalculation is Workbook-wide.** A dependent on another Sheet is recalculated with the rest, and Values are published only when the whole recalculation has completed (ADR-0047).
- **References carry their Sheet** already (ADR-0046/0047). A qualifier naming a Sheet of the Workbook reads it; one naming no Sheet is `#REF!`. Structural changes rewrite References on every Sheet.
- **Sheet operations are `SheetEdit`s of the Workbook** (add, rename, delete, move, copy), each one step. A deletion's step records the Sheet and the Formulas elsewhere that read it, so undoing it restores them exactly.
- **3D References** are built only after their behaviour has been read from Excel (ticket 12); the functions that take one are those Excel accepts it in, among the functions ExSheet supports.
- **The component**: one ExGrid draws the Sheet shown; each Sheet's view state (selection, active cell, scroll offsets, column widths) is kept by the component and restored on showing it. A `Workbook` parameter beside `Document`; both set is refused by name. A tab press, a tab menu command, a rename's commit and Ctrl+PageUp/PageDown are inputs in the Input Order (ADR-0240).
- **Commands name their Sheet** (ADR-0331 §6); one that names none is taken only while there is one Sheet.
- **Pointing** widens ADR-0058's Pointing Scope to the Workbook's Sheets.

## Testing Decisions

- **Seams**: the engine's `Workbook` (layer 1, xUnit) for computation, References across Sheets, Sheet operations and their undo, and the Workbook Document; the `ExSheet` component (layer 2, bUnit) for the tabs, view state, parameters and commands; layer 3 (Playwright) for keys, Input Order at machine speed, focus, pointing across tabs and both Chromes.
- Tests assert behaviour through those public surfaces only, and name their ADR (`ADR-0331`).
- Expected values come from Excel: the existing case corpus style for Formulas, and Windows-run readings for what Excel was asked (ticket 12).
- Prior art: the engine's `Sheet` tests under `dotnet/tests/ExSheet.Engine.Tests`, the component tests under `dotnet/tests/ExSheet.Components`, and ExPivot's details tabs in layer 3.

## Out of Scope

Tab colours, hidden Sheets, grouped Sheets (ADR-0331 §4); several ExSheets sharing one Workbook (§1);
`.xlsx` (ADR-0330, its own spec); Excel Tables.

## Further Notes

The tickets are under `issues/`, worked in number order where their blocking edges allow.
