# Excel's active cell — 2026-09-27, second run

**Scope: Part B of [`verify-on-windows-2.md`](../../docs/specs/exsheet/verify-on-windows-2.md)**,
for ADR-0052 (ExGrid's Focus is Excel's active cell). **Verified commit:
`e9c448aa9d5104d801ec96d66c6c4e59ca0c4823`.** Nothing is decided here. The rules ADR-0052 still
needs are recorded as Excel shows them.

## How it was asked

- Excel 16.0.20326.20158, visible and maximised on the 3840×2160 display at 150%, zoom 100%,
  driven by [`active-cell.ps1`](active-cell.ps1) through the first run's
  [`excel-driver.ps1`](../2026-09-27-windows-excel/excel-driver.ps1). **Every gesture is a real
  one**: keys through `SendKeys` or `keybd_event`, clicks and drags through the Win32 mouse. COM
  only reads the state back once Excel is Ready, and scrolls in the two steps marked "COM
  (scroll)"
- For each step: `Selection.Address` ("Selection"), `ActiveCell` ("Active"), and the window's
  scroll position and `VisibleRange`, which say which cell is kept in view. Every step is in
  [`active-cell.jsonl`](active-cell.jsonl), and screenshots are in [`shots/`](shots/) (`B<case>-…`):
  a crop of the Name Box, the Formula Bar and the cells, and one of the status bar
- **The Name Box during a gesture** comes from screenshots taken while the mouse button or Shift
  was still down, since COM cannot read Excel mid-gesture
- **The setting Enter depends on** (File › Options › Advanced, "After pressing Enter, move
  selection"): **on, direction Down** (`MoveAfterReturn` True, `MoveAfterReturnDirection`
  xlDown, -4121)
- **Case 9 needed the keyboard switched.** The Japanese IME this machine types through takes
  Ctrl+Space for itself (it turns the IME on), so Excel never sees the key. For case 9 only,
  Excel's window was switched to the English (UK) keyboard (`WM_INPUTLANGCHANGEREQUEST`,
  0x08090809), as Win+Space would, and switched back to the Japanese IME (0x04110411) after, with
  the IME off. Both switches are in the log
- Case 11 was asked three times. The first attempt recorded its first two steps and then failed
  on a screenshot, a defect in the script: the crop was fixed at A1:L16, which a Shift+PageDown
  scrolls away. The second attempt ran in the workbook the other cases had used, whose used range
  case 15 had stretched to H62, so its Ctrl+Shift+End went there. The third, in a new workbook,
  is the record, and it agrees with the first attempt's two steps

## 1. Shift+↓ ×2 and Shift+→ from B2, and what scrolls

| Step | Selection | Active | Name Box | View (scroll row, column) |
|---|---|---|---|---|
| click B2 | B2 | B2 | B2 | 1, 1 |
| Shift+↓ | B2:B3 | B2 | | 1, 1 |
| Shift+↓ | B2:B4 | B2 | | 1, 1 |
| Shift+→ | B2:C4 | B2 | **B2** once Shift is up ([`B1-Shift-Right.png`](shots/B1-Shift-Right.png)) | 1, 1 |
| the same, Shift still held | B2:C4 | B2 | **`3R x 2C`** ([`B1-Shift-held-after-Down-Down-Right.png`](shots/B1-Shift-held-after-Down-Down-Right.png)) | |
| click AL56, near the view's bottom-right (view A1:AN58) | AL56 | AL56 | | 1, 1 |
| Shift+↓ ×1..4 | AL56:AL57 … AL56:AL60 | AL56 | | 1 → 2 → 3 → **4**, 1: the view scrolls to keep **the moving end** in view (A4:AN61) |
| Shift+→ ×1..4 | AL56:AM60 … AL56:AP60 | AL56 | | 4, 1 → 2 → 3 → **4** (D4:AQ61) |
| Shift+↑ ×2 | AL56:AP59, AL56:AP58 | AL56 | | 4, 4: no scroll |

**The active cell stays where the extension started. What is kept in view is the moving end, not
the active cell**: the view scrolls as soon as the moving end reaches its edge.

## 2. Drags, D5 to B2 and B2 to D5

| Gesture | During, button down | After release: Selection | Active |
|---|---|---|---|
| drag D5 → B2 (up and left) | Name Box **`4R x 3C`**; D5 unshaded ([`B2-dragging-D5-to-B2-button-down.png`](shots/B2-dragging-D5-to-B2-button-down.png)) | B2:D5 | **D5**, where the drag started |
| drag B2 → D5 | Name Box **`4R x 3C`**; B2 unshaded ([`B2-dragging-B2-to-D5-button-down.png`](shots/B2-dragging-B2-to-D5-button-down.png)) | B2:D5 | **B2** |

## 3. A1:C3, Enter twice, then Shift+→ and Shift+↓

| Step | Selection | Active |
|---|---|---|
| click A1, Shift+click C3 | A1:C3 | A1 |
| Enter | A1:C3 | A2 |
| Enter | A1:C3 | A3 |
| Shift+→ | **A1:D3** | A3 |
| Shift+↓ | **A2:D3** | A3 |
| (again) A1:C3, Enter ×3 | A1:C3 | B1 |
| Shift+↑ | **A1:C2** | B1 |
| (again) A1:C3, Tab, Enter | A1:C3 | B2 |
| Shift+← | **A1:C3**, unchanged | B2 |

**The edge that moves is the one opposite the active cell, in the range as selected.** With A3
active, at the bottom-left, → moved the right edge (C to D) and ↓ moved the top edge (1 to 2).
With B1 active, at the top, ↑ moved the bottom edge (3 to 2). With B2 active in the middle, ←
changed nothing. The active cell never moved.

## 4. Tab, Shift+Tab, Enter and Shift+Enter in A1:C3 (active A1), ten presses each

| Key | Active cell, press 1 → 10 |
|---|---|
| Tab | B1, C1, A2, B2, C2, A3, B3, C3, **A1**, B1: across, then down, wrapping |
| Shift+Tab | **C3**, B3, A3, C2, B2, A2, C1, B1, A1, C3: backwards across |
| Enter | A2, A3, B1, B2, B3, C1, C2, C3, **A1**, A2: down, then across, wrapping |
| Shift+Enter | **C3**, C2, C1, B3, B2, B1, A3, A2, A1, C3: backwards down |
| Enter, from C3:A1 selected by click C3, Shift+click A1 (active C3) | **A1**, A2, A3, B1 |

The Selection stayed A1:C3 throughout. Selecting from C3 made C3 active, and Enter from the last
cell wraps to the first.

## 5. Ctrl+click D5 onto A1:B2, then Shift+↓

| Step | Selection | Active |
|---|---|---|
| A1:B2, Ctrl+click D5 | A1:B2,D5 | D5 |
| Shift+↓ | A1:B2,**D5:D6** | D5 |
| D5, then Ctrl+drag A1:B2 | D5,A1:B2 | A1 |
| Shift+↓ | D5,**A1:B3** | A1 |

**The range that extends is the last one added, the one holding the active cell, and from its
own starting corner.**

## 6. Ctrl+click on a cell already selected

| Step | Selection | Active |
|---|---|---|
| A1:C3, Ctrl+click B2 | **A3:C3,C2,A2,A1:C1**: B2 taken out, the rest as four ranges | A1 |
| Shift+↓ | A3:C3,C2,A2,**A1:C2** | A1 |
| A1:C3, Ctrl+click A1, the active cell | **A2:C3,B1:C1** | **B1** |
| Shift+↓ | A2:C3,**B1:C2** | B1 |
| B2 alone, Ctrl+click B2 | B2: the only cell is not taken out | B2 |

## 7. Shift+Backspace and Ctrl+Backspace

| Step | Selection | Active | View |
|---|---|---|---|
| B2:D4 (active B2), Shift+Backspace | **B2** | B2 | |
| D4:B2 (active D4), Shift+Backspace | **D4** | D4 | |
| B2:D4, scrolled to row 300, column T (COM) | B2:D4 | B2 | 300, 20 |
| Ctrl+Backspace | B2:D4 | B2 | **1, 1**: back to the active cell |
| B2:D4, Enter ×2, scrolled to row 300 (COM) | B2:D4 | B4 | 300, 1 |
| Ctrl+Backspace | B2:D4 | B4 | **1, 1** |

Shift+Backspace collapses the Selection to the active cell. Ctrl+Backspace scrolls to it and
changes nothing else.

## 8. Ctrl+. (period)

| Selection | Active cell, press 1 → |
|---|---|
| A1:C3, active A1 | **C1, C3, A3, A1**, C1: the corners, clockwise |
| A1:C3,E5:F6, active E5 | F5, F6, E6, E5, F5, F6, E6, E5, F5: **the corners of the active range only**, clockwise |

## 9. Ctrl+Space and Shift+Space (English keyboard, above)

| Step | Selection | Active |
|---|---|---|
| C3, Ctrl+Space | C:C | **C3** |
| C3, Shift+Space | 3:3 | **C3** |
| B2:D4, Enter (active B3), Ctrl+Space | **B:D** | **B3** |
| B2:D4, Enter (active B3), Shift+Space | **2:4** | **B3** |

A whole range's columns or rows are selected, and the active cell does not move.

## 10. Ctrl+A

| Step | Selection | Active |
|---|---|---|
| C3 inside data A1:D5, Ctrl+A | A1:D5, the current region | C3 |
| Ctrl+A again | the whole sheet (1:1048576) | C3 |
| G8 outside the data, Ctrl+A | the whole sheet | G8 |

## 11. Ctrl+Shift+End, Ctrl+Shift+Home, Shift+PageDown from B2 (data A1:E10)

| Step | Selection | Active | View |
|---|---|---|---|
| Ctrl+Shift+End | B2:E10: to the used range's last cell | B2 | 1, 1 |
| Ctrl+Shift+Home | A1:B2 | B2 | 1, 1 |
| Shift+PageDown | B2:B59 | B2 | **58**, 1 (A58:AN115) |
| Shift+PageDown | B2:B116 | B2 | **115**, 1 |
| Shift+PageUp | B2:B59 | B2 | 58, 1 |

The view follows the moving end a page at a time. The active cell stays B2, off screen.

## 12. B2:D4 (active B2), type `x`, Enter

| Step | Cells holding a value | Selection | Active |
|---|---|---|---|
| `x`, Enter | **B2** = x | B2:D4 | **B3** |
| `y`, Enter; `z`, Enter | B2 = x, B3 = y, B4 = z | B2:D4 | **C2** |

The entry goes to the active cell only. Enter moves within the Selection, and down the column
first, as in 4.

## 13. B2:D4, type `x`, Ctrl+Enter

**All nine cells, B2:D4, hold `x`.** The Selection stays B2:D4 and the active cell stays B2.

## 14. B2:D4 of a filled A1:E5, Delete

**The nine cells B2:D4 are cleared**, and only they. The Selection and the active cell (B2) are
unchanged.

## 15. After a paste, and after inserting rows over a selected range

| Step | Selection | Active | View |
|---|---|---|---|
| A1:C3 copied (Ctrl+C), pasted (Ctrl+V) onto F6 | **F6:H8**, the pasted block | **F6** | 1, 1 |
| the same pasted onto F56, near the view's bottom (view A1:AN58) | F56:H58 | F56 | 1, 1: no scroll |
| B3:C4 (active B3), Home › Insert › Insert Sheet Rows | B3:C4 | **B3** | |
| C4:B3 (active C4), Insert Sheet Rows | B3:C4 | **C4** | |

After the paste, the Selection is the pasted block, with its first cell active, and the view does
not scroll to show the block. Rows inserted over a selection leave the Selection on the same
addresses (B3:C4), and the active cell where it was in it. What the cells then hold was not read.
