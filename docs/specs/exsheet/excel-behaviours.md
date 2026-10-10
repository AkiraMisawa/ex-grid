# Excel's behaviours, observed beside ExSheet

Status: done — every item was observed beside Excel at 9a78a14 on 2026-09-27, as the first Windows
run's Part D (`verification/2026-09-27-windows-excel/behaviours.md`). Its probes are
`tests/ExGrid.Browser/sheet-vs-excel.spec.mjs`, which the second to fourth runs ran again. *(Set
from the records on 2026-10-10.)*

The engine's Values are checked against Excel automatically, by the case corpus and its oracle
(`verify-on-windows.md`, Part A). This list is about what a user **does**: keys, clicks and drags.
The Claude Code session on the Windows machine drives both sides itself, and falls back to the
person only where it cannot observe the result.

## How to drive it

**Before starting,** tell the user that the run sends real keys and mouse moves, and that they must
not touch the keyboard or mouse until you say it is finished. The screen must stay unlocked, with no
screensaver, and the input method must be off (IME off, direct input). Wait for their go-ahead.

For each item, use the first method that answers it, and record which one you used:

1. **COM**, where Excel's object model has the same meaning as the gesture. Record `COM`.
   - `Range.End(xlDown)` is Ctrl+↓.
   - `Application.Goto` is the Name Box.
   - `Rows(n).Insert()` inserts a row.
   - `ActiveSheet.Name` and `Application.Undo` rename a sheet and undo it.
   - Read back `Selection.Address`, `ActiveCell.Address`, `Formula2`, `Value2`, `Text` and
     `NumberFormat`.
2. **Real keys into the Excel window.** Record `keys`.
   - Bring the window to the foreground with `Application.Visible = $true` and the window's
     handle.
   - Send the keys with `System.Windows.Forms.SendKeys.SendWait` or Win32 `SendInput`.
   - Read the result back through COM.
   - Keys cover Enter mode, F2, Point mode, completion with Tab and Escape, and pasting.
3. **The real mouse and a screenshot.** Record `mouse+screenshot`.
   - Move and drag with Win32 `SendInput`.
   - Take a cell's screen position from `ActiveWindow.PointsToScreenPixelsX/Y` and the
     `Range.Left/Top/Width/Height`.
   - Take a screenshot of the Excel window and read it.
   - Use this for the fill-handle drag, Heading clicks, and what only shows on screen (the
     completion list, the argument hint, the status bar, a value running over its neighbour).
4. **The user**, only when none of the above settles it. Record `asked the user`.
   - Ask one precise question: what to do, and what to report.

**ExSheet's side** is driven with Playwright on the DemoHost's `/sheet` page, in the same way
`tests/ExGrid.Browser` drives the grid: real keys and the real mouse through the browser. Write
these probes as specs under `tests/ExGrid.Browser/` (for example `sheet-vs-excel.spec.mjs`), with
Excel's observed answer as the expectation, so they keep checking after this run. A probe whose
feature is not built yet (see the ticket's `Status:`) is written with `test.fixme` and a note.

**Recording.** Write `verification/<date>-windows-excel/behaviours.md`, one row per item, with
these columns: method, Excel did, ExSheet did, agree?, and a note. Keep the screenshots you relied
on beside it.

**Decide nothing.** Where the two disagree, name the item and the ADR, and leave the choice to the
user. Where ExSheet differs **by decision**, the item says so and names the ADR. Confirm that Excel
really does what the ADR says Excel does, and do not count it as a disagreement.

## Moving and selecting (ADR-0012, ADR-0050)

1. **Ctrl+↓ through data.** Start in a column holding values in rows 1–3, blanks in 4–6, values in
   7–9, then blanks. Starting from A1, press Ctrl+↓ repeatedly and record each stop. Do the same
   from a blank cell, and in a completely empty column.
2. **Ctrl+Shift+→ from inside a row block**: the range selected.
3. **Clicking a column letter, then Shift+clicking another**: what is selected, and where the
   active cell is.
4. **Clicking a row number, then Shift+clicking another**: the same questions.
5. **The corner between the Headings**: what is selected.
6. **Typing `D200` into the Name Box** and pressing Enter: the selection and the scroll position.
   Then type `B2:C5`.

## Entering and editing (ADR-0051, ADR-0012)

7. **Typing into a cell, then pressing an arrow key** (Excel's Enter mode). Does it commit and move?
8. **F2 on a cell holding `=A1*2`.** What does the cell show, where is the caret, and what do the
   arrow keys do?
9. **Point mode by keys.** Type `=`, press ↓ twice, then Shift+→, then type `+`. What is written
   at each step?
10. **Point mode by mouse.** Type `=`, click C3, then drag C3:D4. What is written?
11. **F2 while pointing.** Type `=`, press ↓ (A2 appears), then press F2 and ← twice. Does the caret
    move, or does the pointer?
12. **Point mode from the Formula Bar.** Click into the Formula Bar, type `=`, press ↓.
13. **Completion.** Type `=SU` and record the list. Press ↓, then Tab: what is written? Then Escape
    in the middle of a list: is the list closed, or the edit?
14. **The argument hint** after `=XLOOKUP(`: its exact text, and how the current argument is marked
    as you type commas.
15. **Escape from the Formula Bar** after typing there. Is the edit cancelled, and where is the
    focus?

## Clipboard (ADR-0048, ADR-0050)

16. **Copy `=A1` from B1 and paste it into B3.** What Formula does B3 hold?
17. **Copy a 3×3 block, click one cell, and paste.** What is written, and what is selected
    afterwards?
18. **Copy cells from Excel and paste them into ExSheet**, and the other way round. Use a Formula
    cell, a date, and a number formatted `#,##0.00`. What arrives on each side?
19. **Paste text `=A1+1` and `1,234` from Notepad** into a cell.

## Fill (ADR-0050, item 5)

20. **Drag the fill handle** down from each of these sources, and record the targets' Values, and
    what is selected afterwards:
    - one number
    - two numbers (1, 3)
    - three numbers (1, 2, 4)
    - one date
    - a date with a time
    - `Item 1`
    - `Mon`
    - a Formula `=A1*2`
    - a two-cell pattern, filled up and filled left
21. **Where the handle is**, with several ranges selected (Ctrl+click). Is there a handle at all?

## Structure (ADR-0046)

22. **Insert a row above a selected cell range.** What is selected afterwards, and what formatting
    does the new row have?
23. **Delete a row that a Formula elsewhere references.** What does the Formula show?
24. **Rename the sheet**, then press Ctrl+Z. ExSheet undoes the rename **by decision (ADR-0048)**.
    Record whether Excel undoes it.

## Display (ADR-0016, ADR-0046)

25. **A long text in A1 with B1 empty.** Excel lets the text run over B1. ExSheet cuts it with an
    ellipsis **by decision (ADR-0046)**.
26. **A number too wide for its column**, and a date too wide: `####` in both?
27. **A circular reference** (`A1: =B1`, `B1: =A1`, `C1: =IFERROR(A1,0)`): what each cell shows,
    and the status bar. ExSheet shows `#CIRC!` everywhere **by decision (ADR-0047)**.

## Comments
