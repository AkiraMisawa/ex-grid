# Excel's active cell and scroll steps — 2026-09-28, third run

**Scope: Part B of [`verify-on-windows-3.md`](../../docs/specs/exsheet/verify-on-windows-3.md)**:
what ADR-0052 still extrapolates, and Excel's scroll steps for ADR-0053. **Verified commit:
`20666707a4c8b7b71a899f25ad3e885a02da402c`.** Nothing is decided here; each item is recorded as
Excel shows it.

## How it was asked

- Excel 16.0.20326.20158, visible and maximised on the 3840×2160 display at 150%, zoom 100%,
  regional format en-GB, driven by [`active-cell.ps1`](active-cell.ps1) through the first run's
  [`excel-driver.ps1`](../2026-09-27-windows-excel/excel-driver.ps1) and the second run's
  [`case-guard.ps1`](../2026-09-27-windows-excel-2/case-guard.ps1), 10:37–10:40. **Every gesture
  is a real one**: keys through `SendKeys`, clicks, Ctrl+drags and the fill-handle drag through
  the Win32 mouse, and the wheel through `mouse_event`. COM reads the state back once Excel is
  Ready, and sets the scroll row back to 1 between the scroll steps of item 5
- For each step: `Selection.Address` ("Selection"), `ActiveCell` ("Active") and the scroll
  position. Every step is in [`active-cell.jsonl`](active-cell.jsonl), and screenshots are in
  [`shots/`](shots/) (`B<item>-…`): a crop of the Name Box, the Formula Bar and the cells, and one
  of the status bar
- **The settings Enter and the wheel depend on**: "After pressing Enter, move selection" on,
  direction Down (`MoveAfterReturn` True, `MoveAfterReturnDirection` -4121); Windows' wheel
  setting **3 lines per notch** (`SPI_GETWHEELSCROLLLINES`)
- **Item 4's Ctrl+Space steps switched the keyboard.** As in the second run's case 9, Excel's
  window was switched to the English (UK) keyboard (0x08090809) for them and back to the Japanese
  IME (0x04110411) after, with the IME off. Both switches are in the log
- Ranges are made as a hand makes them: a click, then a Shift+click for the first range, and a
  Ctrl+drag for the second

## 1. Shift+↓ after Enter has cycled back into the earlier range

A1:B2 selected, D4:E5 added by Ctrl+drag (the active cell is D4). Enter moves D5, E4, E5, then A1.

| Step | Selection | Active |
|---|---|---|
| A1:B2, then Ctrl+drag D4:E5 | A1:B2,D4:E5 | D4 |
| Enter ×4 | A1:B2,D4:E5 | **A1** |
| then Shift+↓ | **A1:B3**,D4:E5 | A1 |
| then Shift+→ | **A1:C3**,D4:E5 | A1 |

**The range holding the active cell extends, from the active cell**, and D4:E5 stays as it was.
From the second cell of A1:B2 (Enter ×5, active **A2**), Shift+↓ gives **A2:B2**,D4:E5, active A2:
the corner opposite A2 (B1) moves down one row.

**The screenshots of these two Shift+↓ steps do not show what COM reads**
([`B1-then-Shift-Down.png`](shots/B1-then-Shift-Down.png),
[`B1-then-Shift-Down-from-the-second-cell.png`](shots/B1-then-Shift-Down-from-the-second-cell.png)):
in both, A1:B2 (or B2) is painted darker, as a doubly selected area is, D4:E5 is not shaded, and
the headers of D, E, 4 and 5 are still highlighted. The screenshot is taken about half a second
after the key, once COM answers. Whether Excel repaints later was not looked at.

## 2. The cycling order through two disjoint ranges

Enter ×10, then Tab ×10, then Shift+Enter ×3, in one sequence. The active cell after each key:

| Made in the order | Start | Enter ×10 | then Tab ×10 | then Shift+Enter ×3 |
|---|---|---|---|---|
| A1:B2, then D4:E5 | D4 | D5 E4 E5 **A1** A2 B1 B2 **D4** D5 E4 | D5 E5 **A1** B1 A2 B2 **D4** E4 D5 E5 | E4 D5 D4 |
| D4:E5, then A1:B2 | A1 | A2 B1 B2 **D4** D5 E4 E5 **A1** A2 B1 | A2 B2 **D4** E4 D5 E5 **A1** B1 A2 B2 | B1 A2 A1 |

Enter goes down each range's columns, left to right; Tab goes along each range's rows, top to
bottom; both then go on to the next range in the order the ranges were made, and back to the
first. `Selection.Address` lists the ranges in the order they were made (`A1:B2,D4:E5` or
`D4:E5,A1:B2`). Shift+Enter goes back through the same order. **Tab after Enter starts from the
cell Enter reached**: E4, Tab, is D5 (the next cell of D4:E5 by rows), not E5.

## 3. Ctrl+click on the active cell when it is not the top-left

In A1:C3 with the active cell moved by Enter or Shift+Enter, a Ctrl+click on that active cell
**takes it out, and the active cell goes to A1**. Shift+↓ then extends the fragment holding A1:

| Active before | Reached by | After Ctrl+click on it | Active | then Shift+↓ |
|---|---|---|---|---|
| B2 | Enter ×4 | A3:C3,C2,A2,**A1:C1** | A1 | A3:C3,C2,A2,**A1:C2** |
| C3 | Shift+Enter | A3:B3,**A1:C2** | A1 | A3:B3,**A1:C3** |
| C1 | Enter ×6 | A2:C3,**A1:B1** | A1 | A2:C3,**A1:B2** |
| A3 | Enter ×2 | B3:C3,**A1:C2** | A1 | B3:C3,**A1:C3** |

**A Ctrl+click on a cell of the range that does not hold the active cell** (A1:B2 and D4:E5,
active D4) takes that cell out of its range and leaves the active cell where it is. Shift+↓
extends the range holding the active cell:

| Ctrl+click | Selection | Active | then Shift+↓ |
|---|---|---|---|
| A1 | A2:B2,B1,D4:E5 | D4 | A2:B2,B1,**D4:E6** |
| B2 | A2,A1:B1,D4:E5 | D4 | A2,A1:B1,**D4:E6** |

With the active cell moved by Enter back into A1:B2 (A1), a Ctrl+click on E5 gives
A1:B2,D5,D4:E4 with **the active cell D4**, not A1; Shift+↓ then gives **A1:B2,D5,D4:E5**, active
D4 ([`B3-then-Ctrl-click-E5-then-Shift-Down.png`](shots/B3-then-Ctrl-click-E5-then-Shift-Down.png)
paints D5 darker, doubly selected).

## 4. What the implementation read without Excel

**C3, Ctrl+Space, then Shift+↓ and Shift+→.** Ctrl+Space selects C:C, active C3. **Shift+↓
changes nothing** (C:C, active C3). Shift+→ then gives **C:D**, active C3. Shift+→ alone after
Ctrl+Space gives C:D as well.

**Ctrl+. from a cell on no corner.** In A1:C3:

| Active before | Ctrl+. ×1 | ×2 | ×3 | ×4 | ×5 |
|---|---|---|---|---|---|
| A2 (Enter ×1) | **A1** | C1 | C3 | A3 | A1 |
| B2 (Enter ×4, inside) | **A1** | C1 | C3 | A3 | A1 |

From a cell on no corner, Ctrl+. goes to the top-left corner first, then clockwise.

**The order of the fragments after a take-out, and the order Enter visits them.** A1:C3, then a
Ctrl+click on one cell:

| Taken out | `Selection.Address` (area order) | Active | Enter ×10 |
|---|---|---|---|
| B2 | A3:C3, C2, A2, A1:C1 | A1 | B1 C1 A3 B3 C3 C2 A2 A1 B1 C1 |
| C3 | A3:B3, A1:C2 | A1 | A2 B1 B2 C1 C2 A3 B3 A1 A2 B1 |
| A1 | A2:C3, B1:C1 | **B1** | C1 A2 A3 B2 B3 C2 C3 B1 C1 A2 |

Enter visits the fragment holding the active cell, then the areas in the order
`Selection.Address` lists them, wrapping round. The address lists the fragments bottom to top:
for B2, the row below, then the cells beside it, then the row above.

**B4:B2 made from B4, filled down by the handle to B6** (B2:B4 holding 1, 2, 3): the Selection
becomes **B2:B6** and **the active cell stays B4**; B5 and B6 are filled with 4 and 5
([`B4-B4-B2-from-B4-filled-by-the-handle-to-B6.png`](shots/B4-B4-B2-from-B4-filled-by-the-handle-to-B6.png)).

## 5. Scrolling, for ADR-0053

An empty sheet in a new workbook, zoom 100% (on the display at 150%), 58 rows in view (A1:AN58).
The scroll row is read before and after each gesture:

| Gesture | Scroll row before | After |
|---|---|---|
| **One wheel notch down**, over E10 | 1 | **4** (3 rows) |
| One notch up | 4 | 1 |
| Three notches down | 1 | 10 (9 rows) |
| **One click on the down arrow** | 1 | **2** (1 row) |
| A second click | 2 | 3 |
| **One click on the track below the thumb** | 1 | **58** (57 rows, a page) |
| A second click | 58 | 115 |

The wheel moves Windows' 3 lines per notch, the arrow one row, the track a page (the rows in
view less one). The vertical scroll bar, as UI Automation reports it: 26×1686 px, arrows 26 px,
and **in an empty sheet the thumb fills 1606 of it**, leaving 28 px of track below it.
**The first attempt at the track (10:37) clicked at three quarters of the bar's height, which was
on the thumb, and nothing scrolled.** The script was changed to click the part UI Automation
calls "Page down", and the second attempt (10:38) is the record; both are in the log.
