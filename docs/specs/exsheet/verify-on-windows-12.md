# What to verify on Windows, twelfth run

Status: ready-for-human. **Part A asks Excel only and can run now.** (Part C of the
[eleventh run](verify-on-windows-11.md) still waits for tickets 48, 49 and 51.)

This is for the Claude Code session on the Windows desktop of the earlier runs.
- Read [`verify-on-windows-11.md`](verify-on-windows-11.md) first. Its Setup, method and tools
  apply here unchanged.
- **Decide nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or
  `docs/definition-of-done.md`.

Part A settles the readings in
[ADR-0063](../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "Readings
until the twelfth Windows run". Those readings are about Borders, which ticket 49 is to paint. They
cover three things:
- what other operations do to the line between two cells;
- what an inserted or deleted row leaves on its edges;
- what the formatting keys do to a column's width and to the built-in date and time formats.

## Setup

- **Branches.** Fetch **`claude/exsheet-cell-format`**. Branch **`claude/exsheet-windows-verify-12`**
  from its tip, and record that tip as the verified commit.
- **Authorisation.** The user has authorised this run in advance: real keys and mouse to Excel.
  **Do not stop to ask.** Say "starting" before the first input and "finished" after the last.
- **Tools.** Reuse the eleventh run's `cell-format.ps1`, which is on
  `claude/exsheet-windows-verify-11` and merged into `claude/exsheet-cell-format`. Copy it as
  `verification/<date>-windows-excel-12/cell-format-12.ps1`, taking one case or a list of cases.
- **Keys and COM.** Every operation a case asks about is done with real keys (or the mouse, where it
  says so). COM only sets up a case and reads the result.
- **Edges.** Read every edge as `LineStyle Weight Color` from **both** cells that share it, for
  example `B2` `xlEdgeBottom` and `B3` `xlEdgeTop`. Record which line is drawn, from the pixels
  across the gridline, as the eleventh run's group 3 did.
- **Environment.** Record it as before. Use the regional format en-GB unless a case says otherwise.

## Part A — Excel

Use a fresh workbook for each case, with one sheet named `Sheet1`, at 100% zoom. Record each case as
one JSON line in `cell-format-12.jsonl` and as a row in `cell-format-12.md`.

### Group 1 — the line between two cells, after other operations

These cases settle whether a paste, Ctrl+D, Ctrl+R and the fill handle write the neighbour's side
as setting an edge does (case 7 of the eleventh run).

| # | Set up (COM) | Keys / mouse | What is asked | Reading |
|---|---|---|---|---|
| 1 | C2's left edge thin blue. E5's right edge thick red; E5 = 1 | Select E5, Ctrl+C, select B2, Ctrl+V | B2's right and C2's left, read from both cells; the line drawn | the paste writes C2's left too: thick red, read from both |
| 2 | B2's right edge thick red (so C2's left reads it). E5 = 1, no borders | Select E5, Ctrl+C, select B2, Ctrl+V | B2's right and C2's left; the line drawn | none, read from both |
| 3 | B2 = 1 with its right edge thick red and its bottom edge thick black. C3's left edge thin blue | Select B2:B4, Ctrl+D | Every edge of B3 and B4, C3's and C4's left edges, B5's top edge; the lines drawn | B3 and B4 take B2's right and bottom; C3's and C4's left read thick red |
| 4 | B2 = 1 with its bottom edge thick black and its right edge thick red. E2's left edge thin blue | Select B2:D2, Ctrl+R | Every edge of C2 and D2, C3's and D3's top edges, and E2's left edge; the lines drawn | C2 and D2 take B2's edges, the neighbours read them, and E2's left reads thick red |
| 5 | As in 3 | With the mouse, drag B2's fill handle down to B4 | As in 3 | as Ctrl+D |

### Group 2 — two rows that meet when the row between them is deleted

| # | Set up (COM) | Keys | What is asked | Reading |
|---|---|---|---|---|
| 6 | B2's bottom edge thick black (B3's top edge reads it). B4's top edge thin blue (B3's bottom edge reads it) | Select row 3 (A3, Shift+Space), Ctrl+`-` | B2's bottom and the new B3's top, read from both; the line drawn | unknown: record. ADR-0063 reads the upper row's line as winning until this answer |
| 7 | The same with columns: B2's right edge thick black, D2's left edge thin blue | Select column C (C1, Ctrl+Space), Ctrl+`-` | B2's right and the new C2's left; the line drawn | unknown: record |
| 8 | B2's bottom edge thick black only | Delete row 3 as in 6 | The new B3's top edge, read from both | thick black, read from both |
| 9 | B3's top edge thin blue, set from B3; nothing on B2 | Delete row 2 (A2, Shift+Space, Ctrl+`-`) | B1's bottom and the new B2's top | unknown: record |

### Group 3 — what insertion leaves on the edges

These are readings ticket 55 took. Each is named in its test.

| # | Set up (COM) | Keys | What is asked | Reading |
|---|---|---|---|---|
| 10 | B1's top edge thick black (the Sheet's edge) | Select row 1 (A1, Shift+Space), Ctrl+Shift+`=` | Every edge of the new B1 and of B2 (the old B1) | the line goes; the new row 1 takes nothing |
| 11 | B2's bottom edge thick black, filled yellow | Select rows 3:4 (A3, Shift+Space, Shift+Down), Ctrl+Shift+`=` | B3's top and bottom, B4's top and bottom, B5's top edge; B3's and B4's Fill | B3's top reads the thick line; the edges between the new rows are empty; both new rows are yellow |
| 12 | B2 `abc`: bold, italic, red Font, yellow Fill | Insert a row at 3 as in the eleventh run's case 12 | B3's Font (bold, italic, colour) and Fill | B3 takes the Font and the Fill |
| 13 | B2's right edge thick black, filled yellow | Select column C (C1, Ctrl+Space), Ctrl+Shift+`=` | C2's left and right edges, D2's left edge (the old C2); C2's Fill | C2's left reads the thick line, its right is empty; C2 is yellow |

### Group 4 — outlines over whole rows and the whole Sheet, and inside lines over whole columns

| # | Keys | What is asked | Reading |
|---|---|---|---|
| 14 | Select row 3 (A3, Shift+Space), Ctrl+Shift+`&` | `Rows(3).Borders` (top, bottom, left, right); A3's left edge; XFD3's right edge; B3's top and bottom edges | top and bottom only |
| 15 | Select the whole Sheet (Ctrl+A twice from an empty cell, or the corner box with the mouse), Ctrl+Shift+`&` | A1's top and left edges, XFD1's right edge, A1048576's bottom edge, `Columns(1).Borders(xlEdgeLeft)`, `Columns(16384).Borders(xlEdgeRight)`, `Rows(1).Borders(xlEdgeTop)` | the left of A and the right of XFD only |
| 16 | Select columns B:C (B1, Ctrl+Space, Shift+Right). Ctrl+1, the Border tab, **Inside** (with the mouse), OK | B1's top edge, B2's top edge, B1048576's bottom edge, and the B/C edge in row 5; a screenshot of rows 1 to 3 | unknown at row 1 and row 1048576: record. Ticket 55 reads the inside line on the top of row 1 and the bottom of row 1048576 as well |

### Group 5 — the formatting keys, a column's width, and the built-in date and time formats

| # | Set up | Keys | What is asked | Reading |
|---|---|---|---|---|
| 17 | Columns A to G at the standard width. Values whose formatted text will not fit: A1 = 46000.5, B1 = 1234567.5, C1 = 1234567.5, D1 = 0.123456, E1 = 1234567.5, F1 = 46000 | On each cell in turn, the key from its row: A1 Ctrl+`#` (date), B1 Ctrl+Shift+`$`, C1 Ctrl+Shift+`!`, D1 Ctrl+Shift+`%`, E1 Ctrl+Shift+`^`, F1 Ctrl+Shift+`@` | Each column's width before and after (`ColumnWidth`), and the text shown | unknown: record which keys widen a standard-width column |
| 18 | As 17, with each column set to width 12 by hand first | The same keys | The same | no column widens |
| 19 | Under en-GB, en-US and ja-JP (`Set-Culture`, a new Excel, as in the eleventh run's case 20): A1 = the date 5 January 2026 and A2 = the time 09:05 | Ctrl+`#` on A1 and Ctrl+Shift+`@` on A2 (use whichever key types each character on the layout) | `NumberFormat` / `NumberFormatLocal`, and the text shown (`5-Jan-26` or `05-Jan-26`; `9:05` or `09:05`) | unknown: record whether Excel localises built-ins 15 and 20 |

## Results

- Push `claude/exsheet-windows-verify-12` with `verification/<date>-windows-excel-12/`.
- In `cell-format-12.md`, summarise two lists:
  - every case whose answer differs from its reading;
  - every case marked "record".
- Restore the regional format afterwards, and say so.
