# Verification — 2026-09-27, Windows, Excel as the oracle

**Scope: Part A of [`verify-on-windows.md`](../../docs/specs/exsheet/verify-on-windows.md).**
The engine's case corpus asked of a real Excel through COM, items 2–10, and the ticket 19
table. Part D, Excel's behaviours beside ExSheet, is in [`behaviours.md`](behaviours.md). Part B,
layer 3, is in [`../2026-09-27-windows/results.md`](../2026-09-27-windows/results.md).

**Verified commit: `9a78a14aca7192640f783d7e17cd5eb3e1228d29`**, the tip of
`claude/exsheet-start-8cx3v1` when this run began. The results are committed on
`claude/exsheet-windows-verify`, branched from that commit. Nothing here changes the engine, an
ADR, `CONTEXT.md` or the Definition of Done. Every disagreement is listed for the user to decide.

## Environment

- Windows 11 Pro 25H2 (build 26200), one display, 3840×2160 at 150% (2560×1440 logical). Display language en-GB,
  **regional format en-GB**, system locale (non-Unicode programs) ja-JP
- **Excel: Microsoft 365, version 16.0, build 16.0.20326.20158, 64-bit, Current Channel**, UI
  language English (UK, 2057), "Use system separators" on. `Application.Build` reports only
  `20326`, so the results name EXCEL.EXE's whole version as well
- The oracle ran under Windows PowerShell 5.1.26100 (`powershell.exe`). PowerShell 7 is not
  installed, and the script needs no more than 5.1
- .NET SDK 10 is not on Windows (only SDK 8 is). Build and layers 1–2 ran in WSL2 on the same
  machine, through nix, which is what `AGENTS.md` prescribes there. `dotnet build ExGrid.slnx`
  gave **0 warnings, 0 errors**. `dotnet test ExGrid.slnx` was **green**: ExGrid.Tests 675,
  ExSheet.Engine.Tests 1217, ExGrid.MudBlazor.Tests 71, ExSheet.Components.Tests 138 and
  ExGrid.Components 728 passed, with 1 skipped (ExGrid.Components), 0 failed

**The regional format was changed, with the user's agreement**, for the length of the oracle runs
(`Set-Culture`). The order was en-US (the whole corpus), then de-DE, then ja-JP, then en-GB. The
machine was left on en-GB. `HKCU\Control Panel\International` was exported beforehand, and it
read back identical afterwards, customisations included.

## Part A.1 — the corpus

`tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1`, the whole corpus, with formulas entered
through `Range.Formula2`. There is one results file per regional format:

| File | Regional format | Cases asked | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|
| `results-2026-09-27.json` | en-US | all 820 | 710 | 49 | 32 | 29 |
| `results-2026-09-27-de-DE.json` | de-DE | the 14 that name it | 13 | 1 | 0 | 0 |
| `results-2026-09-27-ja-JP.json` | ja-JP | the 5 that name it | 4 | 1 | 0 | 0 |
| `results-2026-09-27-en-GB.json` | en-GB | the 1 that names it | 1 | 0 | 0 | 0 |
| **The corpus** | | **820** | **728** | **51** | **12** | **29** |

The en-US file's 32 blocked cases include the 20 that name another culture. All 20 are answered
in the other three files, so 12 remain blocked, each by the corpus's own `oracleSkip`.

**Reproducibility.** The `-Update` pass is a second full run, once per regional format. It gave
the same status as the committed results for every case. It then set `source: "observed"` on the
728 agreeing cases (710 + 13 + 4 + 1), and on nothing else. The engine's tests still pass on the
updated corpus (1217 of 1217).

### What the script's first run needed

The script had never been run. Each fix below is in the commit that carries the results, and none
of them changes what a case asks:

1. **`$PSScriptRoot` is empty inside `param()` defaults** under Windows PowerShell 5.1 with
   `-File`. The defaults are now resolved in the body. `-Area a,b` arrives under `-File` as one
   string, and is now split
2. **`Application.Iteration` is refused while no workbook is open.** It is now set per workbook
3. **Every Linked Table case failed** with `InvalidCastException`. PowerShell 5.1's COM binder
   keeps the type of the first value assigned to `Range.Value2`, and refuses a Double after a
   String. `Value2` now goes through `IDispatch` directly
4. **The binder passes the user's locale id, and Excel reads `NumberFormat` in that locale.**
   Under de-DE, `"General"` was refused and `"mmmm"` was taken as minutes, which produced three
   false disagreements (FMT-029, FMT-039, FMT-060). `NumberFormat` now goes through `IDispatch`
   with en-US, as VBA always does. `FormulaLocal` was checked to follow the machine's regional
   format whatever the locale id, which is what a typed entry needs. The en-US run was unaffected,
   since its locale id is en-US
5. **The default column width was assumed to be 8.43.** With Microsoft 365's default font (Aptos
   Narrow) it is **8.09**. `widens` is now compared with the sheet's `StandardWidth`
6. The results name EXCEL.EXE's whole version, since `Application.Build` gives only `20326`

## Disagreements

Each is Excel's answer beside the corpus's expectation. **None was fixed here**, in the engine or
in the case. Whether the engine, the case or an ADR changes is the user's decision. Ticket 19's
rows are marked `(T19 row n)`.

### Arithmetic and text conversion — ADR-0047, tickets 04 and 19

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| ARITH-066 (T19 row 1) | `=0.1+0.2-0.3` | 5.55E-17 | **0** |
| ARITH-067 (T19 row 2) | `=0.5-0.4-0.1` | -2.78E-17 | **0** |
| ARITH-069 (T19 row 4) | `=0.1+0.2=0.3` | FALSE | **TRUE** |
| ARITH-072 (T19 row 6) | `="é">"z"` | TRUE | **FALSE**: Excel sorts é beside e, not by code point |
| ARITH-073 (T19 row 7) | `=1E15&""` | `1E+15` | **`1000000000000000`** |
| ARITH-074 (T19 row 7) | `=1E-5&""` | `1E-05` | **`0.00001`** |
| ARITH-075 (T19 row 7) | `=123456789012345678&""` | `1.23456789012346E+17` | **`123456789012345000`** (Excel stored the constant as `123456789012345000`: 15 digits on entry) |
| ARITH-043 | `=(-8)^(1/3)` | `#NUM!` | **-1.9999999999999998** (Excel takes the real odd root) |

Row 3, `=1*(0.5-0.4-0.1)`, **agrees** at -2.7755575615628914E-17. Excel sets a *final* addition
or subtraction that nearly cancels to 0, and leaves one wrapped in a multiplication alone. This
is ADR-0047's `0.1+0.2-0.3` example, which the ADR says "is under verification".

### IFERROR over an empty cell — ADR-0047, tickets 04 and 19

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| IFERROR-007 | `=IFERROR(A5,"x")`, A5 empty | `""` | **0** |
| IFERROR-008 | `=IFERROR(1/0,A5)`, A5 empty | `""` | **0** |
| IFERROR-012 (T19 row 15) | `=IFERROR(A1,"x")`, A1 empty | `""` | **0** |

The corpus cites Microsoft's documentation for `""`. The Excel observed here gives 0 in all
three.

### XLOOKUP — ADR-0047, tickets 04 and 19

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| XLOOKUP-067 (T19 row 18) | `=XLOOKUP(A1,B1:B3,C1:C3)`, A1 empty, B2 empty | `#N/A` | **2**: the empty lookup value matches the blank B2 |
| XLOOKUP-071 (T19 row 20) | `=XLOOKUP("m",B1:B4,C1:C4,,-1)` over 10, x, 30, y | `#N/A` | **`c`**: numbers count as smaller than any text, so 30 is "the next smaller" |
| XLOOKUP-077 (T19 row 22) | `=XLOOKUP(1,A1:A3,B1:B3,,3)` | `#VALUE!` | **`a`**: match_mode 3 is accepted |

### Typed constants and the format a typed entry takes — ADR-0047, tickets 05 and 19

| Case | Typed | Engine / corpus | Excel |
|---|---|---|---|
| TYPED-021 (T19 row 27) | `$5` | text | **5**, format `$#,##0_);[Red]($#,##0)`, shows `$5 ` |
| TYPED-022 (T19 row 27) | `26-Sep` | text | **46291** (26 Sep 2026), format `d-mmm` |
| TYPED-025 (T19 row 29) | `1,234` | 1234, General | 1234, **format `#,##0`**, shows `1,234` |
| TYPED-026 (T19 row 29) | `1E3` | 1000, General | 1000, **format `0.00E+00`**, shows `1.00E+03` |
| TYPED-027 (T19 row 30), ja-JP | `2026/9/26` | 46291, format `yyyy/mm/dd` | 46291, shows `2026/09/26`, but the **format is Excel's built-in short date** (`m/d/yyyy` in the invariant codes, shown in the system's short-date pattern) |
| ERR-077 | `#GETTING_DATA` | text | **the Error Value** `#GETTING_DATA` (Excel reads it as an error literal) |
| COPY-025, de-DE | pasted text `=SUM(1.5,2)` | 3.5 (invariant syntax in every culture) | **text** `=SUM(1.5,2)`: under de-DE the paste reads the local syntax, which this is not |

TYPED-027's display agrees. What differs is what is stored. Excel records "the system's short
date", which another machine would show in its own pattern. The engine records the pattern
itself.

### Number formats — ADR-0047, tickets 05 and 19

| Case | Value, format | Engine / corpus | Excel |
|---|---|---|---|
| FMT-063 (T19 row 31) | -0.001, `0.00` | `-0.00` | **`0.00`** |
| FMT-065 (T19 row 33) | 0.75, `h:mm am/pm` | `6:00 pm` | **`6:00 PM`**. Excel rewrites the code itself to `h:mm AM/PM` |
| GW-017 | `=1/3` in a column 1 character wide | `####` | **`0`**. General rounds to what fits, down to a bare 0 |

### A Formula's result format — ADR-0047 (second round), ticket 05

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| FF-003 | `=A1-A2`, both dates | shows `1/6/1900`, format `m/d/yyyy` | shows **`6`**, format **General** |
| FF-012 | `=MAX(A1:A2)`, both dates | shows `46292`, General | shows **`9/27/2026`**, format **`m/d/yyyy`** |

Both cases are `uncertain` in the corpus.

### How a Formula is written back — ADR-0047 ("keeps the whitespace it was typed with"), tickets 03, 13 and 08

Excel keeps whitespace between tokens, but not all of it:

| Case | Entered | Engine / corpus | Excel's `Formula2` |
|---|---|---|---|
| TEXT-065 | `=A1+1  ` | kept | **`=A1+1`**: trailing whitespace dropped on entry |
| TEXT-068 | `= Positions[PV] ` | kept | **`= Positions[PV]`** |
| TEXT-070 | `= b2:a1 ` | `= A1:B2 ` | **`= A1:B2`** |
| COPY-019 | `= A1 * $A$1 `, copied | `= B3 * $A$1 ` | **`= B3 * $A$1`** (the trailing space was already gone on entry) |
| NAME-035 | `= Sheet1!A1 + 1 `, sheet renamed | `= 'My Sheet'!A1 + 1 ` | **`= 'My Sheet'!A1 + 1`** |
| STRUCT-032 | `= A5 +  SUM( A1:A5 )⏎* 2 `, row inserted | trailing space kept | **trailing space dropped** |
| TEXT-062 | `=SUM( A1:A3 , 4 )` | kept | **`=SUM( A1:A3, 4 )`**: the space before a comma is dropped |
| TEXT-069 | `= sum( a1 , $b$2 )` | `= SUM( A1 , $B$2 )` | **`= SUM( A1, $B$2 )`** |
| STRUCT-033 | `=  A5  *  C1 `, row 5 deleted | `=  #REF!  *  C1 ` | **`=#REF!  *  C1`**: the whitespace before the deleted token goes with it, and the trailing space too |
| TEXT-064 | `=⇥A1⇥*⇥2` (tabs) | kept | **refused on entry** (COM error 0x800A03EC) |

### How a Reference is written back — ADR-0047, tickets 03, 13 and 16

| Case | Entered or done | Engine / corpus | Excel |
|---|---|---|---|
| TEXT-012 | `=A:XFD` | `=A:XFD` | **`=$1:$1048576`** |
| STRUCT-019 | `=SUM(A1:A1048576)`, row inserted | `=SUM(A1:A1048576)` | **`=SUM(A:A)`**. Excel writes it so from entry on (ticket 13's comment already says so) |
| NAME-013 | `=Sheet1!A5*2`, row 5 deleted | `=#REF!*2` | **`=Sheet1!#REF!*2`** |
| TEXT-021 | `=Positions[[Market Value]]` | kept | **`=Positions[Market Value]`** |
| TEXT-022 | `=Positions[[Rate'#]]` | kept | **`=Positions[Rate'#]`** |
| TABLE-023 | `=SUM(Trades[[Unit Price]])*…` | kept | **`=SUM(Trades[Unit Price])*…`** (Value agrees, 10) |

### Linked Tables — ADR-0049, ticket 16

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| TABLE-011 | `=SUM(Positions[Delta])`, no such column | `#REF!` | **refused on entry** (ticket 16's comment already says Excel refuses) |
| TABLE-013 | `=Positions[PV]`, no such table | `#NAME?` | **refused on entry** |

### Insertion that pushes a Reference off the Sheet — ADR-0046, ticket 13

| Case | Setup, then insert a row at 1 | Engine / corpus | Excel |
|---|---|---|---|
| STRUCT-028 | `=SUM(A2:A1048576)` in B1 | refused (`ReferenceWouldLeaveSheet`) | **done**. The Formula, now in B2, is **`=SUM(A3:A1048576)`**: the range is cut at the edge. The case checks B1, which is empty after the insertion, so the oracle read nothing there. Item 3 below reads B2 |

### A column widening on entry — ADR-0047 (second round), ticket 05

Through COM, Excel never widened a column (WD-001, WD-004, WD-005 and WD-010 disagreed, and the
other six agreed only for that reason). A value set through COM does not widen anything; a typed
one does. So all ten were **typed with real keys** into fresh sheets
([`behaviours.md`](behaviours.md), "Asked by keys"). Typed, **nine agree**, and one does not:

| Case | Typed | Engine / corpus | Excel, typed |
|---|---|---|---|
| **WD-007** | `=123456789*10` | a Formula's result never widens | **widens the column to 10.18** |

For the record, the widths Excel reached: 9.18 (WD-001), 11.18 (WD-004), 8.45 (WD-005), 8.82
(WD-010), and 10.18 (WD-007). The results file keeps COM's answers. The `-Update` pass left the WD
cases' `source` as it was, because COM disagreed.

### A typed constant the keyboard reads differently from COM — ADR-0047, tickets 05 and 19

| Case | Typed | Engine / corpus | Excel through COM | Excel, typed with real keys |
|---|---|---|---|---|
| **TYPED-019** (T19 row 26) | `+A1` | text | text (agrees) | **the Formula `=+A1`** |
| **TYPED-020** (T19 row 26) | `-A1` | text | text (agrees) | **the Formula `=-A1`** |

`Range.FormulaLocal` does not do what the cell editor does with a leading `+` or `-`. The results
file says these two agree, and `-Update` marked them `observed` accordingly. **That marking rests
on COM's answer, which the keyboard contradicts.** It is listed here for the user; the corpus was
not edited by hand.

### Where the engine differs by decision, and the corpus's `excelExpect` is not what Excel does

| Case | Decision | Corpus's `excelExpect` | Excel |
|---|---|---|---|
| ISERROR-010 | ADR-0047, `#CIRC!` | `=ISERROR(A1)` over a cycle is FALSE | **0**. Excel leaves a Formula depending on a cycle uncalculated, at 0 |
| FILL-033 | ADR-0050, a date with a time is refused | 46293.416666666664 (to 9 digits) | **46293.4166666088**. Excel's fill of a date-time steps a day but loses about 5 ms (see item 4) |

## Recorded (the engine differs by decision; Excel's side confirmed)

- **A circular reference** (ADR-0047, `#CIRC!`). Excel shows no error in any case. ERR-083
  `=A1+1` in A1 shows 0. In ERR-078..081 (A1 `=B1+1`, B1 `=A1`, C1 `=A1*2`, D1 `=C1&"x"`) A1
  shows **1**, B1 0, C1 2 and D1 `2x`: one pass of stale values. ERR-084, ERR-093, NAME-009,
  NAME-010 and STRUCT-021 show 0
- **XLOOKUP's binary search where the data is not sorted as the mode says** (ADR-0047).
  XLOOKUP-048..057 answer something, often wrong (`c`, `#N/A`, `d`, `b`). XLOOKUP-057's wildcard
  under binary search is `#VALUE!` in Excel too. XLOOKUP-059..062 (duplicate keys): `b`, `c`, `b`,
  `b`. Item 9 below goes further
- **Fill patterns ExSheet does not take** (ADR-0050). FILL-045: `jan` fills `feb`. FILL-052..054:
  1 with a text, a Formula or a blank below it continues 2 (the numbers form their own series)
- **REF-006** (ADR-0047). `VLOOKUP`, outside the declared set, is `#N/A` in Excel where the engine
  gives `#NAME?`

## Blocked

- **TABLE-005..010**: a Linked Table still waiting for its data has no Excel counterpart
- **NAME-006..008**: a qualifier naming no sheet may open a file dialog in Excel. Asked by hand in
  `behaviours.md` only if it matters; it was not asked here
- **LVL-023, LVL-024, NAME-036**: Excel's undo does not reach changes made through COM. NAME-036
  (undoing a rename) is Part D item 24

## Items 2–10

`probes.ps1` asked items 2, 3, 4, 7, 8 and 9 of Excel through COM; its answers are in
`probes.json`. The engine's answers come from the same inputs given to `ExSheet.Engine` at the
verified commit. Excel's Text column is under en-GB, the machine's own format; Values and Formulas
do not depend on it.

### 2. Formatting of inserted rows and columns

| Setup | Insertion | Excel | Engine |
|---|---|---|---|
| Row 2 formatted `0.00` and filled yellow, as a whole row | `Rows(3).Insert()` | row 3 takes `0.00` **and the fill**, across the whole row (A3 and Z3) | row 3 takes `0.00` (A3 and Z3). The engine has no fill |
| A2 alone formatted and filled | `Rows(3).Insert()` | A3 takes `0.00` and the fill; B3 nothing | A3 takes `0.00` |
| Row 2 formatted and filled | `Rows(2).Insert()`, at the formatted row itself | the new row 2 takes row 1's (none); the formatted row moves to 3 | the same |
| Column B formatted and filled, as a whole column | `Columns(3).Insert()` | column C takes `0.00` and the fill (C1 and C100) | column C takes `0.00` |

**Agrees.** The procedure says "the engine inserts them blank today", and so does ticket 13's first
comment. Both are out of date: commit `bdb4927` ("inserted rows and columns take their neighbour's
formatting") made the engine do what Excel does, and `Sheet.InsertRows` documents it. The fill has
no engine counterpart to compare.

### 3. Inserting where a Reference would be pushed off the Sheet

| Setup | Insertion | Excel | Engine |
|---|---|---|---|
| A value in A1048576, `=A1048576` in B1 | a row at 1 | **refused**: "Microsoft Excel can't insert new cells because it would push non-empty cells off the end of the worksheet. These non-empty cells might appear empty but have blank values, some formatting, or a formula. Delete enough rows or columns to make room for what you want to insert and then try again." | refused, `EntriesWouldLeaveSheet`: "Inserting 1 row at 1 would push A1048576, which holds an Entry, off the Sheet." |
| A1048576 blank, `=A1048576` in B1 | a row at 1 | **done**; the Formula, now in B2, is **`=#REF!`** | **refused**, `ReferenceWouldLeaveSheet` |
| `=SUM(A1:A1048576)` in B1 | a row at 5 | done; Excel writes `=SUM(A:A)` before and after | done; `=SUM(A1:A1048576)` |
| `=SUM(A2:A1048576)` in B1 (STRUCT-028) | a row at 1 | **done**; in B2, **`=SUM(A3:A1048576)`**: cut at the edge | **refused**, `ReferenceWouldLeaveSheet` |
| `=SUM(A5:A1048575)` in B1 | a row at 1 | done; in B2, `=SUM(A6:A1048576)` | the same |
| A1048576 holding only a number format | a row at 1 | done; the format is dropped | the same |

The message is COM's, and it is the text of Excel's own dialog. **Disagreements** (ticket 13,
ADR-0046/0047): where only a Reference would leave the Sheet, Excel does the insertion, writing
`#REF!` for a single cell and cutting a range at the edge. The engine refuses both. The written
form of `A1:A1048576` is listed above (STRUCT-019).

### 4. Fill (`AutoFill`, `xlFillDefault`)

| Source | Excel | Engine |
|---|---|---|
| 5 | 5, 5, 5 | the same |
| a date, 26 Sep 2026 (`m/d/yyyy`) | the next days, same format | the same |
| a date with a time, 26 Sep 2026 10:00 (46291.416666666664) | **46292.4166666088**, 46293.4166666088, 46294.4166666088: a day on, **but the time loses ~5 ms** | refused (`FillPatternNotSupported`, a time of day), by decision (ADR-0050) |
| `Item 1` | **`Item 2`, `Item 3`, `Item 4`** | refused, by decision (ADR-0050) |
| 1, 3 | 5, 7, 9, 11 | the same |
| 1, 2, 4 | **5.33333333333333**, 6.83333333333333, 8.33333333333333, 9.83333333333333, 11.3333333333333 | **5.333333333333334**, 6.833333333333334, 8.333333333333334, 9.833333333333334, 11.333333333333334 |
| 0.1, 0.2, 0.4 | 0.533333333333333, 0.683333333333333, 0.833333333333333, 0.983333333333333, 1.13333333333333 | 0.5333333333333334, 0.6833333333333335, 0.8333333333333335, 0.9833333333333335, 1.1333333333333335 |
| 1, 3 in A5:A6, filled up | -7, -5, -3, -1 (A1..A4) | the same |
| `a`, `b` in A5:A6, filled up | A1..A4: a, b, a, b | the same |
| `a`, `b`, `c` in A4:A6, filled up | A1..A3: a, b, c | the same |
| 1, 3 in E1:F1, filled left | -7, -5, -3, -1 (A1..D1) | the same |
| `a`, `b` in E1:F1, filled left | A1..D1: a, b, a, b | the same |
| `=B1*2`, `x`, a blank, filled to A9 | `=B4*2`, x, blank, `=B7*2`, x, blank | the same |

**Disagreement: the trend's last digits** (ticket 15, ADR-0050). Excel's trend values are
**rounded to 15 significant digits**: the Value2 read back is the double nearest
5.33333333333333, not 5.333333333333334. The engine keeps the full double. Through 15 digits, what
a user sees is the same. 0.9833333333333335 rounds to 0.983333333333334 at 15 digits, where Excel
holds 0.983333333333333, so Excel does not simply round the exact double either.

The backwards repetition of a pattern filled up or left, which ticket 15 reported as uncertain,
**agrees**. So does a source mixing a Formula, text and a blank.

### 5. What is selected after a fill-handle drag

Asked with the real mouse ([`behaviours.md`](behaviours.md), item 20). **The source and the
target**, with the ActiveCell on the source's first cell. Filled up, that is the cell next to the
target, not its top. ExSheet selects the source and the target too (ADR-0050, item 5 refined).
Its Name Box names the Focus, the moving end, where Excel's ActiveCell is the source's first cell.

### 6. A circular reference

Typed with real keys ([`behaviours.md`](behaviours.md), item 27). `A1: =B1`, then `B1: =A1`:
**one dialog** when the cycle closes: "There are one or more circular references where a formula
refers to its own cell either directly or indirectly. This might cause them to calculate
incorrectly. Try removing or changing these references, or moving the formulae to different
cells." [OK]. Then **A1 and B1 show 0**, and `C1: =IFERROR(A1,0)` shows 0 with no second dialog.
The status bar reads **`Circular References: A1`**, and blue tracer arrows join A1 and B1. The
engine shows `#CIRC!` in all three, by decision (ADR-0047). The Values through COM are listed
above under "Recorded".

### 7. Spilled arrays

| Entered | Excel | Engine |
|---|---|---|
| `=A1:A3` in C1 through `Formula2`, 1, 2, 3 in A1:A3 | **spills**: C1:C3 read 1, 2, 3; C2 and C3 hold no Formula of their own | `#VALUE!`, by decision (ADR-0047: spilling is refused, not answered with one value) |
| the same, with `x` already in C3 | **`#SPILL!`** in C1 | `#VALUE!` |
| `=A1:A3` in C2 through `Range.Formula` (the pre-dynamic-array entry) | Excel writes **`=@A1:A3`**, which reads 2 (implicit intersection) | `#VALUE!` |

It confirms what ADR-0047 says Excel does.

### 8. Whitespace

`Range.Formula = "= A1 + B1"` and `Range.Formula2 = "= A1 + B1"` both read back as
**`= A1 + B1`**, with a Value of 3. **Agrees** with the engine (TEXT-074). What Excel drops is
listed under "How a Formula is written back": trailing whitespace, and a space before a comma.

### 9. XLOOKUP's binary search over duplicate keys, and over unsorted data

Ascending data, `search_mode` 2:

| `lookup_array` (B) | Formula | Excel returns the row |
|---|---|---|
| 1, 2, 2, 2, 3, 4 | `=XLOOKUP(2,…,,0,2)` | **2, the first 2** (1 and -1 search modes: the first, 2, and the last, 4) |
| 1, 2, 2 | `=XLOOKUP(2,…,,0,2)` | 2, the first |
| 2, 2, 3 | the same | 1, the first |
| 1, 2, 2, 2, 2, 2, 3 | the same | 2, the first |
| 2, 2, 2, 2, 2, 2, 2, 2 | the same | 1, the first |
| 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 | the same | 3, the first |
| 1, 2, 2, 2, 3, 4 | `=XLOOKUP(2.5,…,,-1,2)` | 4, **the last** 2 (the next smaller) |
| 1, 2, 2, 2, 3, 4 | `=XLOOKUP(1.5,…,,1,2)` | 2, **the first** 2 (the next larger) |

Descending data, `search_mode` -2, over 4, 3, 2, 2, 2, 1:

| Formula | Excel returns the row |
|---|---|
| `=XLOOKUP(2,…,,0,-2)` | **5, the last 2** |
| `=XLOOKUP(2.5,…,,-1,-2)` | 3, the first 2 |
| `=XLOOKUP(1.5,…,,1,-2)` | 5, the last 2 |

In every layout tried, an exact match under ascending binary search returned the first of the
equal keys, and under descending binary search the last. Whether that holds for every length is
not proven by eight layouts.

Unsorted data, 3, 1, 4, 1, 5, 9, 2, 6 (rows 1–8):

| Formula | Excel |
|---|---|
| `=XLOOKUP(4,…,,0,2)` | `#N/A`, although 4 is in row 3 |
| `=XLOOKUP(1,…,,0,2)` | row 2 |
| `=XLOOKUP(9,…,,0,2)` | row 6 |
| `=XLOOKUP(2,…,,0,2)` | `#N/A`, although 2 is in row 7 |
| `=XLOOKUP(4,…,,0,-2)` | `#N/A` |
| `=XLOOKUP(1,…,,0,-2)` | row 4 |
| `=XLOOKUP(6,…,,0,-2)` | `#N/A` |

The engine answers `#VALUE!` in all of these. It refuses binary search over duplicate keys and over
unsorted data, by decision (ADR-0047). Excel answers unsorted data silently, and sometimes with
`#N/A` for a key that is present. Ascending duplicates gave the first match in every layout tried.

### 10. Pasting unreadable Formula text

Plain text `=1+` on the clipboard, as Notepad puts it there, pasted with Ctrl+V
([`behaviours.md`](behaviours.md), item A10). **Excel took it as the text `=1+`**, with no refusal
and no dialog. ExSheet refuses the whole paste, and its cell stays empty. Ticket 14 reported this
for a decision, and this is the answer it asked for (ADR-0048).

## The ticket 19 table

Filled in [`issues/19-verify-against-excel.md`](../../docs/specs/exsheet/issues/19-verify-against-excel.md)
from the results files, with the Excel build in its Comments.
