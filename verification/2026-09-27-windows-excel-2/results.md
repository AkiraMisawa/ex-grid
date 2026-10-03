# Verification — 2026-09-27, Windows, Excel as the oracle, second run

**Scope: Part A of [`verify-on-windows-2.md`](../../docs/specs/exsheet/verify-on-windows-2.md).**
The whole case corpus asked of a real Excel again, through COM and with real keys. Part B, the
active cell, is in [`active-cell.md`](active-cell.md). Parts C and D are in
[`../2026-09-27-windows-2/results.md`](../2026-09-27-windows-2/results.md), and Part E, the bisect,
is in [`../2026-09-27-windows-bisect/results.md`](../2026-09-27-windows-bisect/results.md).

**Verified commit: `e9c448aa9d5104d801ec96d66c6c4e59ca0c4823`**, the tip of
`claude/exsheet-start-8cx3v1` when this run began. The results are committed on
`claude/exsheet-windows-verify-2`, branched from that commit. Nothing here changes the engine, an
ADR, `CONTEXT.md` or the Definition of Done. Every disagreement is listed for the user to decide.
The corpus changed only where `-Update` changed it: `source` on the agreeing cases.

## Environment

- The same machine as the first run: Windows 11 Pro 25H2 (build 26200), one display, 3840×2160 at
  150%, display language en-GB, **regional format en-GB**, system locale ja-JP. The keyboard for
  Excel's window is the Japanese IME (Microsoft IME), kept off (alphanumeric) throughout
- **Excel: Microsoft 365, 16.0.20326.20158, 64-bit**, the same build as the first run
- Windows PowerShell 5.1 drives Excel. Build and layers 1–2 ran in WSL2 through nix, as in the
  first run. `dotnet build ExGrid.slnx` gave **0 warnings, 0 errors**. `dotnet test ExGrid.slnx`
  was **green**: ExGrid.Tests 681, ExSheet.Engine.Tests 1586, ExGrid.MudBlazor.Tests 75,
  ExSheet.Components.Tests 211 and ExGrid.Components 778 passed, with 1 skipped
  (ExGrid.Components), 0 failed
- **The regional format was changed under the user's advance authorisation** (`Set-Culture`):
  en-US from 18:32 for the whole corpus, then de-DE at 22:03, ja-JP at 22:04 and en-GB at 22:04,
  where the machine was left. `HKCU\Control Panel\International` was exported at 18:32, before
  the first change ([`international-before.reg`](international-before.reg)), and again at 22:04,
  after the last ([`international-after.reg`](international-after.reg)). **The two are identical,
  byte for byte** (SHA-256 `071c2b4716c2b1afdad10629c20cc64d99291bf7ec6466fcb1cc8b923456b1af`),
  customisations included. The first export is also identical to the first run's

## Part A — the corpus

`tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1`, the whole corpus under en-US, then the cases
that name de-DE, ja-JP and en-GB under those formats. **Every one was asked twice**: through COM,
as the first run did (formulas through `Range.Formula2`, constants through `Range.FormulaLocal`),
and with real keys (`-Keys`, below). One results file per format and route:

| File | Regional format | Entered through | Cases | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|---|
| `results-2026-09-27-run2.json` | en-US | COM | 1056 | 927 | 40 | 64 | 25 |
| `results-2026-09-27-run2-keys.json` | en-US | keys | 1056 | 923 | 39 | 69 | 25 |
| `results-2026-09-27-run2-keys-dialogs.json` | en-US | keys | the 33 that raised a dialog | 19 | 4 | 0 | 10 |
| `results-2026-09-27-run2-de-DE.json` | de-DE | COM | the 16 that name it | 14 | 2 | 0 | 0 |
| `results-2026-09-27-run2-keys-de-DE.json` | de-DE | keys | the 16 that name it | 14 | 2 | 0 | 0 |
| `results-2026-09-27-run2-ja-JP.json` | ja-JP | COM | the 5 that name it | 5 | 0 | 0 | 0 |
| `results-2026-09-27-run2-keys-ja-JP.json` | ja-JP | keys | the 5 that name it | 5 | 0 | 0 | 0 |
| `results-2026-09-27-run2-en-GB.json` | en-GB | COM | the 3 that name it | 3 | 0 | 0 | 0 |
| `results-2026-09-27-run2-keys-en-GB.json` | en-GB | keys | the 3 that name it | 3 | 0 | 0 | 0 |

**The corpus has 1056 cases, 236 more than the first run's 820.** None was removed. Of the first
run's 49 disagreements, 41 now agree (the corpus took Excel's observed answers), 4 still disagree
(FILL-033, ISERROR-010, TABLE-011, TABLE-013), and 4 (WD-001, WD-004, WD-005, WD-010) are now
asked by keys only, and agree there. XLOOKUP-059..062, recorded in the first run, now agree.
**No case that existed in the first run disagrees now where it did not then.** All 36 new
disagreements through COM are among the 236 new cases.

The en-US files' blocked cases include the 24 that name another format. All 24 are answered in
the other files. What stays blocked in every file:

- **TABLE-005..010** (6): a Linked Table still waiting for its data has no Excel counterpart
- **CW-021, CW-022, CW-023, CW-025, CW-026** (5): a width an entry widened the column to
  (`automatic`), which nothing but an entry sets
- Through COM only, and answered below: the 13 WD cases and TYPED-019, 020, 028–032 (asked by
  keys); NAME-006..008, NAME-036, CW-013, CW-014, LVL-023, LVL-024 and LVL-030 (asked by hand)
- With keys only, and answered through COM: 23 XLOOKUP cases with more than 20 cells of their own
  (XLOOKUP-110, XLOOKUP-123..144), not typed; TEXT-064 and TEXT-079, whose Formula holds a tab,
  which the keyboard cannot type into a cell (Tab moves to the next cell)

**Reproducibility, and `-Update`.** The `-Update` pass is a second COM run, once per regional
format. It gave **the same status and the same answer as the first COM run for every one of the
1080 cases asked** (1056 + 16 + 5 + 3). It then set `source: "observed"` on the 949 agreeing
cases (927 + 14 + 5 + 3), of which **147 changed**: 140 from `uncertain` and 7 from
`documented` (CW-001..005, CW-015, CW-017). Nothing else in the corpus changed. The engine's
tests still pass on the updated corpus (1586 of 1586). `-Update` rests on COM's answers: six of
the cases it confirmed are ones the keyboard contradicts (listed under "COM and the keyboard").

### The keyboard pass, and what it needed

`-Keys` types each case's own cells, and its `enter` actions, into a visible, maximised Excel, one
Unicode character at a time through `SendInput`, a line break as Alt+Enter, and commits with
Enter. Formulas are typed as well under en-US, the syntax the corpus is written in; under the
other formats they go in through `Formula2`, and only constants are typed. Excel's alerts are on
while an entry is typed, as they are for a user. Fixtures, tables, formats and the other actions
still go in through COM. A case with more than 20 cells of its own is not typed.

The first attempts stalled on Excel's dialogs, for minutes. The trial of 20:47 stopped for 13
minutes on TEXT-078. Excel had raised "There's a problem with this formula" and the script had
answered it, but a second dialog of Excel's then stood in front, visible but never painted. Excel's
main window was disabled, the dialog ignored Enter, and the script waited inside a COM call. An
earlier run had waited 18 minutes in the same way. The script was therefore changed, in the commit
that carries these results, and none of the changes alters what a case asks:

1. **A watcher on a thread of its own answers Excel's dialogs** (`OracleKeys.Dialogs` in
   `oracle.ps1`). It finds a dialog by its window (a visible top-level window of Excel's process,
   of class `NUIDialog`, `#32770` or `bosa_sdm*`), waits 300 ms, and reads its text through UI
   Automation, on another thread and for at most two seconds. It then presses the default
   button. After "There's a problem with this formula" it also presses Escape, which cancels the
   edit Excel goes back to, and the entry counts as refused
2. **Every step of a case has a time**: 20 seconds to type an entry, 30 to set up or apply the
   actions, 60 to calculate and read the answer. An Excel that overruns, or whose dialog does not
   close after Enter, Escape and `WM_CLOSE`, is ended and started again, and the case is blocked
   with what was seen. **In the runs recorded here no Excel was ended**
3. The script makes no COM call while a dialog is up, and asks whether Excel answers COM through
   `Workbooks.Count`. `ActiveSheet` is null between cases, when no workbook is open

The full keyboard pass then took 26 minutes (21:19–21:45). The slowest case took 13.5 seconds.
**In that run the watcher could not read the text of any dialog within its two seconds**, in all
33 cases that raised one. The same 33 cases run alone at 21:53 had every text read in about 0.1
second, with **the same status and the same answer in every case**
(`results-2026-09-27-run2-keys-dialogs.json`). The cause was not found. Isolated trials with 250
workbooks opened and closed first, with the clipboard used first, and with COM polled during the
read all read the text in 0.1 second. So the dialogs' texts below come from that file. In the full
run the three entries Excel refused after "There's a problem with this formula" are recorded as
"stayed in Edit mode with no dialog", because a dialog whose text was not read could not be told
apart. The script now says "after the dialog it raised" in that case.

**The dialogs the keyboard met**, in 33 cases, all answered with their default button:

| Dialog | Cases | Outcome |
|---|---|---|
| "There are one or more circular references where a formula refers to its own cell either directly or indirectly…" (Warning, OK) | ERR-078..093, IF-015, IFERROR-010, ISERROR-010, NAME-009, NAME-010, STRUCT-021..023, TYPED-019, TYPED-020 (26) | the entry stands |
| "Excel ran out of resources while attempting to calculate one or more formulas. As a result, these formulas cannot be evaluated." (Error, OK) | TEXT-012 (`=A:XFD`), TEXT-013 (`=1:1048576`), TEXT-082 (`=A1:XFD1048576`) | the entry stands |
| "There's a problem with this formula. Not trying to type a formula? …" (Warning, OK) | TEXT-078 (`=A1 +1`, a no-break space), TABLE-011 (`=SUM(Positions[Delta])`), TABLE-013 (`=Positions[PV]`) | Excel goes back to the edit, with the part it cannot read selected ([`shots/A-TABLE-011-after-the-dialog.png`](shots/A-TABLE-011-after-the-dialog.png)); Escape cancels it: **refused** |
| "We found a typo in your formula and tried to correct it to: =E1308. Do you want to accept this correction?" (Yes, No) | TEXT-103 (`=1E308`) | Yes, the default: the cell holds **`=E1308`**, which is 0 |

## Disagreements

Each is Excel's answer beside the corpus's expectation. **None was fixed here**, in the engine or
in the case. Where the keyboard's answer differs from COM's, both are given. None of these cases
carries a ticket 19 row; each is listed under the ADR its corpus area names and the ticket that
area belongs to.

### Arithmetic, comparison and number-to-text — ADR-0047, ticket 04

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| ARITH-076 | `=(0.1+0.2-0.3)` | 0: parentheses leave the subtraction final | **5.5511151231257827E-17**: Excel sets a final subtraction to 0 only outside parentheses |
| ARITH-077 | `=SUM(0.1,0.2,-0.3)` | 5.55E-17: SUM is not a final operation | **0** |
| ARITH-078 | `=1+3E-15-1` | 0: operands within 2^-48 | **3.1086244689504383E-15** |
| ARITH-082 | `=1+4E-15=1` | FALSE: further apart than 2^-48 | **TRUE** |
| ARITH-087 | `="co-op"<"coop"` | TRUE: a hyphen sorts before a letter | **FALSE** |
| ARITH-088 | `="ß">"z"` | TRUE: ß sorts after z | **FALSE** |
| ARITH-092 | `=1E-10&""` | `1E-10` | **`0.0000000001`** |
| ARITH-093 | `=1.23456789012345E-5&""` | `0.0000123456789012345` | **`1.23456789012345E-05`** |
| ARITH-098 | `=8^(1/3)` | 2: the correctly rounded power | **1.9999999999999998** |

### Error Values typed into a cell — ADR-0047 (a typed newer Error Value is text), ticket 03

**Here COM and the keyboard give different answers.** Through `FormulaLocal` every one of these
is text. Typed, Excel reads `#SPILL!`, `#CALC!`, `#FIELD!`, `#BLOCKED!`, `#CONNECT!` and
`#UNKNOWN!` as **Error Values**, and `#spill!` as the Error Value `#SPILL!`. **`#BUSY!` stays
text** even typed.

| Case | Typed | Engine / corpus | Excel through COM | Excel, typed |
|---|---|---|---|---|
| ERR-096 | `#SPILL!` | text (`expect`, the engine's answer) | text (agrees) | **the Error Value** |
| ERR-097 | `#CALC!` | text (`expect`) | text (agrees) | **the Error Value** |
| ERR-098..102, ERR-104 | `#SPILL!`, `#CALC!`, `#FIELD!`, `#BLOCKED!`, `#CONNECT!`, `#UNKNOWN!` | the engine: text; `excelExpect`: the Error Value | **text** | the Error Value (agrees) |
| ERR-103 | `#BUSY!` | the engine: text; `excelExpect`: the Error Value | **text** | **text** |
| ERR-105 | `#spill!` | the engine: text as typed; `excelExpect`: `#SPILL!` | **text `#spill!`** | `#SPILL!`, the Error Value (agrees) |
| ERR-106 | `#CALC!`, then `=ISERROR(A1)` | the engine: FALSE; `excelExpect`: TRUE | **FALSE** | TRUE (agrees) |

`-Update` marked ERR-096 and ERR-097 `observed` on COM's answer, which the keyboard contradicts.

### IFERROR over an empty cell — ADR-0047, ticket 04

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| IFERROR-013 | `=IFERROR(A1,"x")&""`, A1 empty | `"0"`: the 0 is a number | **`""`** |

### XLOOKUP's regular expressions (match_mode 3) — ADR-0047, ticket 04

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| XLOOKUP-097 | `=XLOOKUP("^\w+$",B1:B2,C1:C2,,3)` over `café`, `cafe` | `b`: `\w` is ASCII | **`a`**: `é` is a word character |
| XLOOKUP-100 | `=XLOOKUP("(?=b)b",…,3)` | `#VALUE!`: a lookahead is refused | **`b`**: accepted |
| XLOOKUP-101 | `=XLOOKUP("\p{L}",…,3)` | `#VALUE!`: a Unicode property is refused | **`a`**: accepted |
| XLOOKUP-102 | `=XLOOKUP("(a)\1",…,3)` over `aa`, `b` | `#VALUE!`: a backreference is refused | **`a`**: accepted |

### How a Formula is written back — ADR-0047, ticket 03

| Case | Typed | Engine / corpus | Excel |
|---|---|---|---|
| TEXT-083 | `=sheet1!#ref!*2` | `=sheet1!#REF!*2`: the qualifier as typed | **`=Sheet1!#REF!*2`** |
| TEXT-092 | `=1E20` | `=1E+20` | **`=100000000000000000000`** |
| TEXT-093 | `=1.5E20` | `=1.5E+20` | **`=150000000000000000000`** |
| TEXT-096 | `=1E-10` | `=1E-10` | **`=0.0000000001`** |
| TEXT-097 | `=1.23456789012345E-9` | `=0.00000000123456789012345` | **`=1.23456789012345E-09`** |
| TEXT-103 | `=1E308` | `=1E+308` | COM: **refused** (0x800A03EC). Typed: **the typo dialog**, whose default makes it **`=E1308`**, 0 |
| TEXT-077 | `=A1`, CR LF, `+1` | kept as typed, `=A1\r\n+1` | COM: **refused**. Typed with Alt+Enter: **`=A1\n+1`**, value 1: a line feed alone |

### The General format's display — ADR-0047, ticket 05

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| GW-026 | `=-1/3` in a column 2 characters wide | `0`: no minus sign on a bare 0 | **`-0`** |
| GW-027 | `=-1/3` in a column 1 character wide | `0`: it fits | **`####`** |

### Typed constants — ADR-0048, ticket 05

| Case | Typed | Engine / corpus | Excel through COM | Excel, typed |
|---|---|---|---|---|
| TYPED-047 | `-$5` | text | **the number -5** | **the number -5** |
| TYPED-032 | `- item one` | text | (asked by keys only) | **the Formula `=- item one`, `#NAME?`**, with no dialog |
| DATE-013 | `1/1/30` | 10959 (1930) | 10959 (agrees) | **47484 (2030)** |
| TYPED-040, de-DE | `26-Okt` | shows `26-Okt` | **shows `26. Okt`** (a date, 46321, format `d-mmm`) | the same |

DATE-013's `observed` rests on COM's answer, which the keyboard contradicts.

### A Formula's result format — ADR-0047, ticket 05

| Case | Setup | Engine / corpus | Excel through COM | Excel, typed |
|---|---|---|---|---|
| FF-020 | `=SUM(A1:A2)` over two dates | General, `92576` | **`m/d/yyyy`, `6/17/2153`** | the same |
| FF-011 | `=A1*1` over a typed date | General, `46291` | General (agrees) | **`m/d/yyyy`, `9/26/2026`** |
| FF-013 | `=A1+2*3` over a typed date | General, `46297` | General (agrees) | **`m/d/yyyy`, `10/2/2026`** |

Through COM the date in A1 is set by `FormulaLocal` and the Formula by `Formula2`, and Excel
formats only FF-020's result. Typed, it formats all three.

### Column widths — ADR-0046 and ADR-0047, ticket 05

| Case | Typed | Engine / corpus | Excel through COM | Excel, typed |
|---|---|---|---|---|
| CW-018 | `1234567890` in B2 | widens, and the width is not custom | **does not widen** (8.09) | widens (10.18), and **`customWidth` is set** in the saved file |

### Format levels — ADR-0046 and ADR-0047, tickets 05 and 13

| Case | Setup | Engine / corpus | Excel through COM | Excel, typed |
|---|---|---|---|---|
| LVL-015 | row 2 formatted `0%`, a row inserted at 3, `0.5` entered in B3 | `50%` | `50%` (agrees) | **`1%`**: the value is **0.005**. Excel's automatic percent entry reads a number typed into a percent cell as a percentage |

### Number formats — ADR-0047, ticket 05

| Case | Format | Engine / corpus | Excel |
|---|---|---|---|
| FMT-075 | `0;[Color 10]-0` | accepted, as a named colour is | **refused**: "Unable to set the NumberFormat property of the Range class" |

### Linked Tables — ADR-0049, ticket 16

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| TABLE-011 | `=SUM(Positions[Delta])`, no column Delta | `#REF!` | **refused**: COM 0x800A03EC; typed, "There's a problem with this formula" |
| TABLE-013 | `=Positions[PV]`, no table Positions | `#NAME?` | **refused**, as TABLE-011 |

Both disagreed the same way in the first run.

### Pasting under de-DE — ADR-0048, ticket 14

| Case | Paste | Engine / corpus | Excel |
|---|---|---|---|
| COPY-025, de-DE | the text `1.234,5` then `=SUM(1.5,2)` | 3.5: invariant syntax in every culture | **the text `=SUM(1.5,2)`**, as in the first run |

### Where the engine differs by decision, and the corpus's `excelExpect` is not what Excel does

The same two as the first run, with the same answers:

| Case | Decision | Corpus's `excelExpect` | Excel |
|---|---|---|---|
| ISERROR-010 | ADR-0047, `#CIRC!` | FALSE | **0**: a Formula depending on a cycle is left uncalculated |
| FILL-033 | ADR-0050, a date with a time is refused | 46293.416666666664 | **46293.4166666088**: the fill steps a day and loses about 5 ms |

## COM and the keyboard

Every case whose status or answer differs between `results-2026-09-27-run2.json` (COM) and
`results-2026-09-27-run2-keys.json` (keys). The 23 cases the keyboard did not type are left out.

**Status differs:**

| Case | COM | Keys | What differs |
|---|---|---|---|
| DATE-013 | agree | **disagree** | `1/1/30`: 1930 through COM, 2030 typed |
| ERR-096, ERR-097 | agree | **disagree** | `#SPILL!`, `#CALC!`: text through COM, Error Values typed |
| LVL-015 | agree | **disagree** | `0.5` into a `0%` cell: 0.5 through COM, 0.005 typed |
| FF-011, FF-013 | agree | **disagree** | the result of a Formula over a typed date: General through COM, `m/d/yyyy` typed |
| ERR-098..102, ERR-104, ERR-105 | disagree | agree | typed newer Error Values: text through COM, Error Values typed |
| ERR-106 | disagree | agree | `=ISERROR(A1)` over a typed `#CALC!`: FALSE through COM, TRUE typed |
| WD-001..013 | blocked | agree (13) | asked by keys only; COM never widens a column |
| TYPED-019, 020, 028..031 | blocked | agree (6) | asked by keys only: `+A1`, `-A1`, `-1-2`, `-abc` become Formulas |
| TYPED-032 | blocked | **disagree** | `- item one` becomes `=- item one`, `#NAME?` |
| TEXT-064, TEXT-079 | agree | blocked | a tab cannot be typed into a cell |

**The same status, another answer:**

- **The typed entry widened its column, where COM's did not** (39 cases, every one with the same
  status both ways): ARITH-068, 076, 078, 079, 080; CW-018 (and its `customWidth` is set);
  DATE-001..005, 020, 022, 023, 026; ERR-077; FILL-027..029, 033; FF-001, 002, 004, 012, 016..020,
  022; TEXT-094, 095, 097; FMT-012; TYPED-003, 011, 037, 039, 046. The widths reached were 8.45
  (a date), 8.91, 9.27, 10.18, 11.18, 11.82, 13.45 and 13.55. ERR-101, 102 and 104, above,
  widened too (9.73, 10.18, 10.91)
- **A Formula's result took a format from what was typed**: ARITH-006 `=10+50%` shows
  **`1050.0%`**, format `0.0%` (COM: `10.5`, General); ARITH-064 `=A1*A1` over a typed `1E+300`
  takes **`0.00E+00`** (COM: General)
- **The refusal is reported differently** for TEXT-077, TEXT-078, TEXT-103, TABLE-011 and
  TABLE-013. Through COM, `Formula2` throws 0x800A03EC. Typed, Excel raises the dialogs above.
  TEXT-077 is refused through COM but taken typed, and TEXT-103 is refused through COM but
  corrected to `=E1308` typed

## Asked by hand

[`by-hand.ps1`](by-hand.ps1) asked the nine cases the oracle cannot ask through COM, with Excel's
own UI and real keys and mouse. Its steps are in [`by-hand.jsonl`](by-hand.jsonl) and its
screenshots in [`shots/`](shots/). The three screenshots of the file dialog are cut down to its
title bar, since the dialog opens on the user's own Documents folder. **All nine agree with the
corpus.**

| Case | How | Excel | Corpus |
|---|---|---|---|
| NAME-006 | `=Sheet1!A1` typed on a sheet named My Sheet, alerts on | **a file dialog, "Update Values: Sheet1"**; Escape; the Formula stays `=Sheet1!A1`, `#REF!` | `#REF!` (agrees) |
| NAME-007 | `=Sheet2!A1` typed | the file dialog "Update Values: Sheet2"; Escape; `#REF!` | `#REF!` (agrees) |
| NAME-008 | `='Sheet1 '!A1` typed | the file dialog "Update Values: Sheet1 "; Escape; `#REF!` | `#REF!` (agrees) |
| NAME-036 | renamed to Data (Home › Format › Rename Sheet), then Ctrl+Z | B1 `=Data!A1`, then back to `=Sheet1!A1`, 5 | `=Sheet1!A1`, 5 (agrees) |
| CW-013 | column B set to 20 (Home › Format › Column Width), then Ctrl+Z | 20, then 8.09, the standard width | the standard width (agrees) |
| CW-014 | column B set to 20, deleted (Home › Delete › Delete Sheet Columns), then Ctrl+Z | 20 again | 20 (agrees) |
| LVL-023 | B2 `0%` (Ctrl+Shift+5), column B set to Number through its header and the ribbon, then Ctrl+Z | `0.00` on the column, then B2 `50%`, `0%` | `50%`, `0%` (agrees) |
| LVL-024 | row 2 `0%` through its header, row 2 deleted, then Ctrl+Z | B2 `50%`, `0%`, and E2 `0%` | `50%`, `0%` (agrees) |
| LVL-030 | B2 `0%`, then column B and row 2 (Ctrl+click) set to Number at once, then Ctrl+Z | B2 `50%`, `0%`; B5 and E2 General | `50%`, `0%` (agrees) |

The first attempt at these, at 21:55, is not the record. It was made with Excel's alerts off, so
the file dialog did not appear. It also selected column B with Ctrl+Space, and **the Japanese IME
took Ctrl+Space for itself**: it turned the IME on, the text typed next went into the IME, and
LVL-023, LVL-024 and LVL-030 failed. The IME was turned off again (`VK_IME_OFF`). The script now
types with alerts on and selects columns and rows by clicking their headers. The record above is
its second attempt, at 21:58, which left the IME off.

## Recorded (the engine differs by decision; Excel's side confirmed)

The same as the first run, less XLOOKUP-059..062, which now agree:

- **A circular reference** (ADR-0047, `#CIRC!`): ERR-078..081, 083, 084, 093, NAME-009, NAME-010,
  STRUCT-021. Excel's answers are those of the first run: one pass of stale values, or 0
- **XLOOKUP's binary search over data not sorted as the mode says** (ADR-0047): XLOOKUP-048..057
- **Fill patterns ExSheet does not take** (ADR-0050): FILL-045, FILL-052..054
- **REF-006** (ADR-0047): `VLOOKUP`, outside the declared set, is `#N/A` in Excel where the engine
  gives `#NAME?`
