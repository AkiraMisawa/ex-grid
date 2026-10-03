# Excel's behaviours, observed beside ExSheet — 2026-09-27

**Scope: Part D of [`verify-on-windows.md`](../../docs/specs/exsheet/verify-on-windows.md)**,
following [`excel-behaviours.md`](../../docs/specs/exsheet/excel-behaviours.md), and Part A's items
5, 6 and 10, which need the real screen. **Verified commit:
`9a78a14aca7192640f783d7e17cd5eb3e1228d29`.** Nothing is decided here. Each disagreement names its
ADR and ticket, for the user.

## How it was driven

- **The user was told first**, and confirmed the screen was unlocked, the IME off and their hands
  off the keyboard and mouse. The session then drove Excel itself
- **Excel**: Microsoft 365, 16.0.20326.20158, visible and maximised on the 3840×2160 display at
  150%, driven from Windows PowerShell 5.1 by [`excel-driver.ps1`](excel-driver.ps1). It sends keys
  through `SendKeys`, drives the mouse through Win32 `SetCursorPos`/`mouse_event`, reads state back
  through COM once Excel is in Ready mode, and takes screenshots with `CopyFromScreen`. Mid-edit
  states, which COM cannot read, are read from the screenshots in [`shots/`](shots/). Each item
  names its method: **COM**, **keys**, or **mouse+screenshot**. **No item needed the user.** The
  driver works only in a workbook it made and marked, and closes it unsaved
- **The Excel instance used for Part D had started while the regional format was still en-US**,
  during Part A's oracle runs, and it kept en-US's settings throughout: MDY dates, `.` for decimals,
  `,` for thousands and lists (`Application.International`). `/sheet` is en-US too, so the two
  sides read typed input the same way. The oracle's own results are unaffected, since each of its
  runs starts a fresh Excel
- **ExSheet's side**: [`tests/ExGrid.Browser/sheet-vs-excel.spec.mjs`](../../tests/ExGrid.Browser/sheet-vs-excel.spec.mjs),
  driven by Playwright on `/sheet` with real keys and the real mouse. Each probe's expectation is
  Excel's answer below. Where ExSheet differs by decision, the probe asserts the decision and says
  what Excel does. A probe whose ticket is not `done` is `test.fixme`. `EXGRID_SHEET_UNBUILT=1`
  runs those too, and the "ExSheet did" column reports that run for them
- **Item 18** needs both applications and the one clipboard, so it is a script,
  [`clipboard-probe.mjs`](clipboard-probe.mjs). It keeps the browser open while Excel pastes

### Two things about the method that change what an answer means

- **Excel's ActiveCell is not ExGrid's Focus.** After Shift+click or Shift+arrow, Excel's
  ActiveCell, which the Name Box shows and the Formula Bar edits, stays at the **fixed** end. ExGrid
  calls that end the Anchor. ExGrid's Focus is the **moving** end (ADR-0012, `CONTEXT.md`), and
  ExSheet's Name Box and Formula Bar show the Focus. The probes compare Excel's ActiveCell with
  **what ExSheet's Name Box shows**, which is what a user reads. That comparison is where most of
  the disagreements below come from
- **Probes are paced like a person, 150 ms per key or click.** Unpaced, on the Server host, a
  value typed straight after a click can land in the wrong cell. That is a finding of its own
  (below), and it is kept out of the answers to "what does Excel do"

## Results

Runs of `sheet-vs-excel.spec.mjs`, paced, on Chrome 153 and Edge 154 (identical on both):

| Run | Log | Passed | Failed | Skipped (fixme) |
|---|---|---|---|---|
| WebAssembly | `sheet-vs-excel-wasm.log` | 26 | 12 | 30 |
| Server | `sheet-vs-excel-server.log` | 26 | 12 | 30 |
| WebAssembly, `EXGRID_SHEET_UNBUILT=1` | `sheet-vs-excel-unbuilt.log` | 38 | 26 | 4 (items 18 and 24, which have nothing a spec can drive) |

Every failure in the default runs is one of the ActiveCell rows below: items 2, 3, 4 and 5, and two of item 20, on both browsers. The observation run adds items 9 and 10 (the Name Box while pointing), 16, 17 and 19 (the clipboard), and 22 (the ActiveCell).

### Moving and selecting (ADR-0012, ADR-0050)

| # | Method | Excel did | ExSheet did | Agree? | Note |
|---|---|---|---|---|---|
| 1 | keys (and COM `End(xlDown)`, the same) | Ctrl+↓ from A1 over 1–3, blank 4–6, 7–9: **A3 → A7 → A9 → A1048576**. From blank A5: A7 → A9 → A1048576. Empty column: B1048576. Ctrl+↑ from A9: A7 → A3 → A1 | the same stops in column F | **yes** | Excel's first pass read stale cells because the driver's Alt tap had put the ribbon in KeyTips mode. It was fixed and rerun |
| 2 | keys | Ctrl+Shift+→ from D2 in C2:G2: **D2:G2, ActiveCell D2**. Again: D2:XFD2. Ctrl+Shift+← from D2: C2:D2 | the same Selections, at F6 in E6:I6. **The Name Box shows I6**, the moving end | **Selection yes; ActiveCell no** | ADR-0012 (Focus is the moving end), ADR-0050/0051 (the Name Box and Formula Bar show the Focus) |
| 3 | mouse+screenshot | Column B's letter: **B:B, ActiveCell B1**. Shift+click D: **B:D, ActiveCell B1**. D then Shift+B: B:D, ActiveCell D1. Scrolled to row 100: ActiveCell **D100**, the top visible row | Selections the same, and D100 the same. **Name Box D1** after B then Shift+D, **B1** after D then Shift+B | **Selection yes; ActiveCell no** | As item 2. The top-visible-row anchoring agrees (ADR-0050 §1) |
| 4 | mouse+screenshot | Row 2's number: **2:2, ActiveCell A2**. Shift+click 5: 2:5, A2. 5 then Shift+2: 2:5, A5. Scrolled to column C: ActiveCell C103, the leftmost visible column | Selections the same. **Name Box A5** and **A2**, the moving end | **Selection yes; ActiveCell no** | As item 2. Column A is pinned on `/sheet`, so the scrolled case has no counterpart |
| 5 | mouse+screenshot | The corner: every cell, **ActiveCell A1**. Scrolled to row 100, column C: ActiveCell **C100**, the top-left visible cell | Every cell. **The Name Box stays on the cell selected before** (C3) | **Selection yes; ActiveCell no** | ADR-0050 §1 says the corner selects all and says nothing of the ActiveCell. ExGrid keeps Anchor and Focus, as `SelectAll` does for Ctrl+A (ADR-0012) |
| 6 | mouse+keys (the real Name Box) | `D200` ⏎: D200 selected, **scrolled only until D200 is on screen**, near the bottom (ScrollRow 144, rows 144–201). `B2:C5` ⏎: B2:C5, ActiveCell B2, **row 2 now the first row** | the same: D200 at the bottom of the Viewport, then B2:C5 with row 2 at the top | **yes** | |

### Entering and editing (ADR-0051, ADR-0012)

| # | Method | Excel did | ExSheet did | Agree? | Note |
|---|---|---|---|---|---|
| 7 | keys | `abc` then → : committed, moved to D1. `5` then ↓: committed, moved to C3 | the same | **yes** | |
| 8 | keys+screenshot | F2 on `=A1*2`: the cell shows `=A1*2`, **caret at the end**, status **Edit**, A1 outlined. ←← moves the caret (`=A1│*2`). **↑ moves the caret to the start** and stays in Edit | the same, ↑ included, in the paced runs | **yes** | An unpaced run once left the caret where it was after ↑ (Chrome, WebAssembly). The paced runs never did |
| 9 | keys+screenshot | From C5: `=` → `=`; ↓ → `=C6` (status Point, marching ants, **the Name Box names C6**); ↓ → `=C7`; Shift+→ → `=C7:D7`; `+` → `=C7:D7+`, and the Name Box names C5 again | the same text, from E5 (`=E6`, `=E7`, `=E7:F7`, `=E7:F7+`). **The Name Box stays on E5 while pointing** | **text yes; Name Box no** | Ticket 11 is not `done`, so the probe is fixme. Run with `EXGRID_SHEET_UNBUILT=1`, it fails only on the Name Box. ADR-0051 |
| 10 | mouse+screenshot | `=`, click C3: `=C3`, **the Name Box names C3**. Drag C3→D4: `=C3:D4`. Excel adds a value tooltip, `{3,0;0,4}` | `=C3`, then `=C3:D4`. **The Name Box stays on E10** | **text yes; Name Box no** | Ticket 11, as above |
| 11 | keys+screenshot | `=` ↓ gives `=C6` in **Point**. F2 turns the status to **Edit**, and ←← then moves **the caret** (`=│C6`), not the pointer | the same | **yes** | Ticket 11, as above |
| 12 | mouse+keys+screenshot | In the Formula Bar: `=` then ↓. **Still `=`, status Edit**: the bar does not point | the same | **yes** | Ticket 11, as above |
| 13 | keys+screenshot | `=SU`: SUBSTITUTE (selected), SUBTOTAL, SUM, SUMIF, SUMIFS, SUMPRODUCT, SUMSQ, SUMX2MY2, … with a description tip. ↓ selects SUBTOTAL. Tab writes **`=SUBTOTAL(`**. `=SUM` lists SUM first; Tab writes `=SUM(` and the hint `SUM(number1, [number2], ...)`. **Escape with the list open closes only the list**; the edit stays (`=SU`), and a second Escape cancels it | `=SUM` offers SUM first and selected, Tab writes `=SUM(`, and Escape closes the list and then the edit | **yes** | ExSheet lists only its declared functions (ADR-0047), so `=SU` starts with SUM there and SUBSTITUTE in Excel. Ticket 10 is not `done`: fixme, passes when run |
| 14 | keys+screenshot | `=XLOOKUP(`: **`XLOOKUP(lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode])`**, the current argument in bold. At match_mode, a value list: `0 - Exact match`, `-1 - Exact match or next smaller item`, `1 - Exact match or next larger item`, `2 - Wildcard character match`, **`3 - Regex match`** | the same text, with the current argument marked | **yes** | Ticket 10, as above. **`3 - Regex match` explains XLOOKUP-077** (Part A): match_mode 3 is a mode in this Excel (ADR-0047, ticket 04) |
| 15 | mouse+keys+screenshot | Typed `xyz` in the Formula Bar, Escape: the edit is cancelled, the status is Ready, and **the keys are the grid's again** (`7` ⏎ goes into C5) | the same | **yes** | |

### Clipboard (ADR-0048, ADR-0050)

| # | Method | Excel did | ExSheet did | Agree? | Note |
|---|---|---|---|---|---|
| 16 | keys | `=A1` in B1, Ctrl+C, B3, Ctrl+V: **`=A3`** | **the Value** (`12` for `=B2` pasted from E1 to E3) | **no** | Ticket 14: "Blocked on the core: the copy half". ExSheet cannot recognise its own copy, so a paste is taken as text. ADR-0048. Fixme |
| 17 | keys+screenshot | A 3×3 block onto E5: **E5:G7 selected, ActiveCell E5**, and `=A2*2` written `=E6*2` | the Selection and the Name Box agree (C6:E8, C6). **The Formula arrives as its Value** | **Selection yes; Formulas no** | As item 16 |
| 18 | keys + Playwright ([`clipboard-probe.mjs`](clipboard-probe.mjs)) | **Excel → ExSheet**: a Formula, a date and `#,##0.00`. Excel's clipboard carries the **shown text** in both flavours: `6`, `9/26/2026` and `1,234.50` in text, and the same shown text in HTML (no `x:num`). ExSheet wrote 6, **9/26/2026 as a date**, and **1234.5 without the format**. With Excel's column too narrow, the date was `########` on screen and on the clipboard, **and ExSheet wrote the text `########`**. **ExSheet → Excel**: ExSheet's HTML carries the unformatted invariant Values (`24`, `46291`, `1234.5`), and Excel read the HTML. **The date arrived as the number 46291 and the number as 1234.5, both General** | as described | **no** | ADR-0048 and ticket 14 (the copy half; ExSheet's HTML is invariant Values by design, ADR-0016). Note: a date copied from a too-narrow Excel column becomes the text `########` in ExSheet, not a date and not a refusal. ADR-0016's rule keeps `####` off ExGrid's own copy, and says nothing of it arriving |
| 19 | keys (plain text on the clipboard, as from Notepad) | `=A1+1` → **a Formula** (11). `1,234` → 1234 **with the format `#,##0`** (shows `1,234`). `=SUM(1.5,2)` → a Formula (3.5) | `=A1+1` a Formula. `1,234` → **1234, General**. `=1+` → see Part A item 10 | `=A1+1` **yes**; `1,234` **no** | `1,234` is TYPED-025 of Part A again (ADR-0047, ticket 05). Fixme (ticket 14) |
| A10 | keys | Pasted text **`=1+` became the text `=1+`**: no refusal, no dialog | **the cell stays empty**: the paste is refused whole, as ticket 14's comments describe | **no** | Ticket 14 reported this for a decision ("what Excel does with such a field is not pinned"). This answers it. ADR-0048 |

### Fill (ADR-0050, item 5)

| # | Method | Excel did | ExSheet did | Agree? | Note |
|---|---|---|---|---|---|
| 20 | mouse+screenshot | One number 5: 5, 5, 5 | 5 | **yes** | |
| 20 | mouse | 1, 3: 5, 7, 9, 11; **source and target selected (A1:A6), ActiveCell A1** | 11; E1:E6 selected. **Name Box E2** | **Values and Selection yes; ActiveCell no** | As item 2 |
| 20 | mouse | 1, 2, 4: **5.33333333333333**, 6.83333333333333, … (15 digits held) | the bar shows `5.33333333333333` and `6.83333333333333` | **yes, as shown** | The engine holds 5.333333333333334 underneath (Part A item 4) |
| 20 | mouse | A date, 9/26/2026: the next days | 9/29/2026 at the fourth cell | **yes** | |
| 20 | mouse | A date with a time: the next days at 10:00, **each about 5 ms short** (…​.4166666088) | refused, with the notice | **no, by decision** | ADR-0050: an unimplemented pattern is refused. Excel's answer confirmed |
| 20 | mouse | `Item 1`: Item 2, 3, 4. `Mon`: Tue, Wed, Thu | refused | **no, by decision** | ADR-0050, as above |
| 20 | mouse | `=A1*2`: `=A2*2`, `=A3*2`, `=A4*2` | `=B4*2` at the third cell from `=B2*2` | **yes** | |
| 20 | mouse | 1, 3 filled up (from A5:A6): -1, -3, -5; **ActiveCell A5, the source's first cell** | -1 … -5. **Name Box E6** | **Values yes; ActiveCell no** | As item 2 |
| 20 | mouse | `a`, `b` filled left: `b`, `a`, `b` (the cell next to the source takes its last element) | the same | **yes** | Ticket 15 reported the backwards repetition as uncertain; it agrees |
| 21 | mouse+screenshot | One range: the handle at its corner. **Two ranges (Ctrl+click): no handle**. A drag from the old corner only selects | no handle with two ranges | **yes** | |
| A5 | mouse | What is selected after a fill-handle drag: **source and target**, ActiveCell on the source's first cell | source and target (ADR-0050, item 5 refined) | **yes** (the ActiveCell as in item 2) | |

### Structure (ADR-0046)

| # | Method | Excel did | ExSheet did | Agree? | Note |
|---|---|---|---|---|---|
| 22 | keys (Home › Insert › Insert Sheet Rows) | B3:C4 selected: **two rows inserted above row 3**, the Selection **stays B3:C4** (now the new rows), ActiveCell B3. The new rows take the **row above's formatting** across the row. `=B3` → `=B5` | through the Context Menu: the Selection stays B3:C4, and the rows below moved. **Name Box C4** | **yes, but the ActiveCell** | Ticket 13 is not `done`: fixme, observed with `EXGRID_SHEET_UNBUILT=1`. The formatting agrees (Part A item 2) |
| 23 | keys (Home › Delete › Delete Sheet Rows) | Row 5 deleted: `=A5*2` → **`=#REF!*2`**, `=SUM(A4:A6)` → **`=SUM(A4:A5)`**, `=A5` → `=#REF!` | `=#REF!*2`; `=SUM(B2:B3)` from `=SUM(B2:B4)` | **yes** | Ticket 13, as above |
| 24 | keys (Home › Format › Rename Sheet) | Renamed to `Data`, then Ctrl+Z: **`Sheet1` again. Excel undoes a rename** | `/sheet` offers no rename to drive | **Excel matches ADR-0048's decision** | NAME-036 of Part A, answered here |

### Display (ADR-0016, ADR-0046)

| # | Method | Excel did | ExSheet did | Agree? | Note |
|---|---|---|---|---|---|
| 25 | keys+screenshot | A long text in A1, B1 empty: **runs over B1 and C1**, and stops at a filled cell. Beside a filled cell it is cut, **with no ellipsis** | cut with an ellipsis | **no, by decision** | ADR-0046. Excel's answer confirmed |
| 26 | keys+screenshot | At a 4-character width: `123456` (General), `12345` as `0.00` and a date **all `####`** | `123456789012` as `#,##0.00` in a default column: `####` | **yes, for the number** | `/sheet` cannot narrow a column (ExSheet declares no `OnColumnWidthChanged`), so the date's case is not asked there |
| 27 | keys+screenshot | `A1: =B1`, `B1: =A1`: **one dialog** when the cycle closes (below). All three cells, including `C1: =IFERROR(A1,0)`, **show 0**. Status bar: **`Circular References: A1`**, and blue tracer arrows between A1 and B1. No second dialog for C1 | `#CIRC!` in all three | **no, by decision** | ADR-0047. Excel's answer confirmed |
| A6 | as item 27 | The dialog: "There are one or more circular references where a formula refers to its own cell either directly or indirectly. This might cause them to calculate incorrectly. Try removing or changing these references, or moving the formulae to different cells." [OK] | | | Excel's UI language is English (UK), hence "formulae" |

### Asked by keys because COM could not answer (Part A)

| Case | Method | Excel did | Engine | Agree? |
|---|---|---|---|---|
| WD-001 | keys, a fresh sheet | 9 digits **widen** the column to 9.18 | widens | **yes** |
| WD-002, 003, 006, 008, 009 | keys | 8 digits, `1.23456789` (shows 1.234568), text, `12.5%`, `1234567.891` (shows 1234568): **no widening** | no widening | **yes** |
| WD-004 | keys | 12 digits: widens to 11.18 and shows `1.23457E+11` | widens | **yes** |
| WD-005 | keys | `9/26/2026`: widens to 8.45 | widens | **yes** |
| WD-010 | keys | `-12345678`: widens to 8.82 | widens | **yes** |
| **WD-007** | keys | **`=123456789*10` widens the column to 10.18** | a Formula's result never widens | **no** (ADR-0047 second round, ticket 05) |
| TYPED-019/020 (T19 row 26) | keys | **Typed `+A1` becomes the Formula `=+A1`; `-A1` becomes `=-A1`.** Typed `+5` is 5 | text | **no.** COM's `FormulaLocal` returned text and "agreed"; the keyboard does not (ADR-0047, ticket 19) |
| TEXT-074 (T19 row 35) | keys+screenshot | `= A1 + B1 ` typed: stored `= A1 + B1`, the trailing space dropped. F2 shows `= A1 + B1` | spaces kept | **yes** (the trailing space as in Part A's "How a Formula is written back") |

The widths are Excel's own character units at the default font, Aptos Narrow 11. The engine's
`widthOnEntry` is in its units, and is not compared.

## A value typed straight after a click can land in the wrong cell

Found while running this spec on the Server host before it was paced, and reduced to
[`typing-probe.mjs`](typing-probe.mjs) and [`typing-probe-2.mjs`](typing-probe-2.mjs). Click F1,
type `1`, Enter; click F2, type `2`, Enter; click F3, `3`; click F7, `7`. The pause between steps
was varied, three trials each:

| Host | 0 ms | 30 ms | 60 ms | 100, 150, 200 ms |
|---|---|---|---|---|
| Server, loopback, no latency injected (direct and through the latency proxy) | wrong | wrong | wrong | right |
| WebAssembly | wrong | right | right | right |

"Wrong" means a value in the row below the one clicked: `2` in F3, `7` in F8. The cell clicked
after an Enter is selected before the Enter's move lands, and the move then carries the Focus
past it. A single click-type-Enter is right on both hosts at any pause (`typing-probe.mjs`). With
a real network's round trip on the Server host, the window would be wider than 60 ms. ED-22
(ADR-0010) promises that keys are neither lost nor reordered, and its test covers keys only; a
click between keys is outside it. **Listed for the user; nothing was changed.**

## Files

- [`excel-driver.ps1`](excel-driver.ps1): the Excel side's driver
- [`clipboard-probe.mjs`](clipboard-probe.mjs), [`typing-probe.mjs`](typing-probe.mjs),
  [`typing-probe-2.mjs`](typing-probe-2.mjs): the probes that are not specs, with their outputs
  `clipboard-probe*.json` and `typing-probe*.json`
- `sheet-vs-excel-*.log`: the spec's runs
- [`shots/`](shots/): the screenshots relied on, named by item
