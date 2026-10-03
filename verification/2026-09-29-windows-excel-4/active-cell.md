# Excel's answers the implementation still reads — 2026-09-29, fourth run

**Scope: Part B of [`verify-on-windows-4.md`](../../docs/specs/exsheet/verify-on-windows-4.md).**
**Verified commit: `393eb61146f10fade1f8c09d0432ff0dc6ee0f03`.** Nothing is decided here; each
item is recorded as Excel shows it, beside what the procedure says ExSheet reads.

## How it was asked

- Excel 16.0.20326.20158, visible and maximised on the 3840×2160 display at 150%, zoom 100%,
  regional format en-GB, driven by [`active-cell.ps1`](active-cell.ps1) through the first run's
  [`excel-driver.ps1`](../2026-09-27-windows-excel/excel-driver.ps1) and the second run's
  [`case-guard.ps1`](../2026-09-27-windows-excel-2/case-guard.ps1), 10:56–10:57. **Every gesture
  is a real one**: keys through `SendKeys`, clicks and Ctrl+drags through the Win32 mouse. COM reads
  the state back once Excel is Ready. No Excel was ended by the guard
- For each step: `Selection.Address` ("Selection"), its areas in order, `ActiveCell` ("Active"), the
  scroll position and the cells named. Every step is in [`active-cell.jsonl`](active-cell.jsonl),
  and screenshots are in [`shots/`](shots/) (`B<item>-…`): a crop of the Name Box, the Formula Bar
  and the cells, and one of the status bar
- The settings Enter depends on, as in the third run: `MoveAfterReturn` True, direction Down
  (-4121); Windows' wheel setting 3 lines
- Ranges are made as a hand makes them: a click, then a Shift+click, and a Ctrl+drag for a second
  range. Nothing in this part sends Ctrl+Space, so the keyboard was not switched

## 1. A take-out of the whole range made last

| Step | Selection | Active | Areas |
|---|---|---|---|
| A1:B2, Ctrl+click F6 | A1:B2,F6 | F6 | A1:B2 F6 |
| **Ctrl+click F6 again** | **A1:B2** | **A1** | A1:B2 |
| then Shift+↓ | A1:B3 | A1 | A1:B3 |
| A1:B2, Ctrl+drag D4:E5 | A1:B2,D4:E5 | D4 | A1:B2 D4:E5 |
| **Ctrl+drag D4:E5 again**, over the same cells | **A1:B2** | **A1** | A1:B2 |
| then Shift+↓ | A1:B3 | A1 | A1:B3 |

**Taking out the whole range made last leaves the earlier range, with its top-left cell A1
active**, in both ways of making it. This is what ExSheet reads ("the latest range still standing
takes its place", ADR-0052): the active cell is A1 in both. Shift+↓ then extends A1:B2 from A1.

## 2. Ctrl+Enter from a Focus that is not the top-left

`=B2+$A$1` typed with B2:C3 selected, then Ctrl+Enter, in an empty sheet. The Formulas as
`Range.Formula2` reads them:

| Made by | Active | B2 | C2 | B3 | C3 |
|---|---|---|---|---|---|
| click C3, Shift+click B2 | C3 | `=A1+$A$1` | `=B1+$A$1` | `=A2+$A$1` | `=B2+$A$1` |
| click B2, Shift+click C3, Enter | B3 | `=B1+$A$1` | `=C1+$A$1` | `=B2+$A$1` | `=C2+$A$1` |

**Excel writes the Formula as typed into the active cell and shifts its relative References into
every other cell from there**; `$A$1` stays. The Selection and the active cell are unchanged by
Ctrl+Enter. This is what ExSheet reads (ADR-0050, SH-27). ExSheet's side is in
[`../2026-09-29-windows-4/results.md`](../2026-09-29-windows-4/results.md), Part C.

## 3. A Formula pasted as plain text over a range

A file holding the text `=A1` (three characters, no line break) was opened in Notepad; Ctrl+A and
Ctrl+C there put it on the clipboard as **Text, UnicodeText** (and Windows' own
`EnterpriseDataProtectionId`), nothing of Excel's. Then, in Excel, B2:C3 selected (click B2,
Shift+click C3), Ctrl+V:

| | Selection | Active | B2 | C2 | B3 | C3 |
|---|---|---|---|---|---|---|
| before | B2:C3 | B2 | | | | |
| **after Ctrl+V** | **B2** | B2 | **`=A1`** (a Formula, shows 0) | empty | empty | empty |

**Excel pasted the text into B2 alone, as the Formula `=A1`, and the Selection became B2**; the
other three cells stayed empty, and the paste options button showed beside B2
([`B3-Ctrl-V-of-the-text-A1-from-Notepad.png`](shots/B3-Ctrl-V-of-the-text-A1-from-Notepad.png)).
ExSheet writes the same text into each cell of the Selection (only a Ctrl+Enter shifts). The
procedure records this and expects nothing.

**About Notepad.** Notepad was not running before this step. Opening the file started it, and
Notepad restored its last session, so a tab of the user's own was also open; Ctrl+W closed only
this file's tab, and Notepad was then closed with a window close (no prompt came). The log's
record of the window in front afterwards names that tab; its title is left out of
[`active-cell.jsonl`](active-cell.jsonl).
