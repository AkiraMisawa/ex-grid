# Verification — 2026-09-29, Windows, Excel as the oracle, fourth run

**Scope: Part A of [`verify-on-windows-4.md`](../../docs/specs/exsheet/verify-on-windows-4.md).**
The whole case corpus asked of a real Excel again, through COM and with real keys, with the cases
the third run's decisions added. Part B is in [`active-cell.md`](active-cell.md), and Parts C and D
in [`../2026-09-29-windows-4/results.md`](../2026-09-29-windows-4/results.md). Part E was not run.

**Verified commit: `393eb61146f10fade1f8c09d0432ff0dc6ee0f03`**, the tip of
`claude/exsheet-start-8cx3v1` when this run began, for every part. The results are committed on
`claude/exsheet-windows-verify-4`, branched from that commit. Nothing here changes the engine, an
ADR, `CONTEXT.md` or the Definition of Done. Every disagreement is listed for the user to decide.
The corpus changed only where `-Update` changed it: `source` on 12 agreeing cases.

## Environment

- The machine of the first three runs: Windows 11 Pro 25H2 (build 26200), one display, 3840×2160
  at 150%, display language en-GB, **regional format en-GB**, system locale ja-JP. The keyboard for
  Excel's window is the Japanese IME, kept off
- **Excel: Microsoft 365, 16.0.20326.20158, 64-bit**, the build of the first three runs
- Windows PowerShell 5.1 drives Excel. Build and layers 1–2 ran in WSL2 through nix at the verified
  commit: `dotnet build ExGrid.slnx` gave **0 warnings, 0 errors**; `dotnet test ExGrid.slnx` was
  **green**: ExGrid.Tests 784, ExSheet.Engine.Tests 1764, ExGrid.MudBlazor.Tests 79,
  ExGrid.Components 863 (1 skipped), ExSheet.Components.Tests 244 passed, 0 failed
- `oracle.ps1` is the verified commit's, unchanged: the one with `widthAtLeast` (CW-028)
- **The regional format was changed under the user's advance authorisation** (`Set-Culture`):
  en-US at 10:12 for the whole corpus, then de-DE at 10:53, ja-JP at 10:54 and en-GB at 10:54,
  where the machine was left. `HKCU\Control Panel\International` was exported at 10:12, before the
  first change ([`international-before.reg`](international-before.reg)), and at 10:55, after the
  last ([`international-after.reg`](international-after.reg)). **The two are identical, byte for
  byte** (SHA-256 `071c2b4716c2b1afdad10629c20cc64d99291bf7ec6466fcb1cc8b923456b1af`), and
  identical to the third run's two exports
- **The two-digit-year setting has not changed**: the export holds no `TwoDigitYearMax` under
  `Calendars`, as in the third run, so it was not recorded again
- The AutoRecovered workbook in `%APPDATA%\Microsoft\Excel` was backed up first and is unchanged

## Part A — the corpus

`tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1`, the whole corpus under en-US, then the cases
that name de-DE, ja-JP and en-GB under those formats, **each through COM and with real keys**, then
`-Update`, as in the third run.

| File | Regional format | Entered through | Cases | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|---|
| `results-2026-09-29-run4.json` | en-US | COM | 1163 | 1023 | 5 | 110 | 25 |
| `results-2026-09-29-run4-keys.json` | en-US | keys | 1163 | 1060 | 7 | 71 | 25 |
| `results-2026-09-29-run4-keys-dialogs.json` | en-US | keys | the 34 that raised a dialog | 20 | 4 | 0 | 10 |
| `results-2026-09-29-run4-de-DE.json` | de-DE | COM | the 17 that name it | 16 | 1 | 0 | 0 |
| `results-2026-09-29-run4-keys-de-DE.json` | de-DE | keys | the 17 that name it | 16 | 1 | 0 | 0 |
| `results-2026-09-29-run4-ja-JP.json` | ja-JP | COM | the 5 that name it | 5 | 0 | 0 | 0 |
| `results-2026-09-29-run4-keys-ja-JP.json` | ja-JP | keys | the 5 that name it | 5 | 0 | 0 | 0 |
| `results-2026-09-29-run4-en-GB.json` | en-GB | COM | the 4 that name it | 4 | 0 | 0 | 0 |
| `results-2026-09-29-run4-keys-en-GB.json` | en-GB | keys | the 4 that name it | 4 | 0 | 0 | 0 |

(The files are in `tests/ExSheet.Engine.Tests/ExcelOracle/`.)

**The corpus has 1163 cases, 48 of them `uncertain`**, as the procedure says: 19 more than the
third run's 1144 (ARITH-136..145, FF-030..035, FMT-078, XLOOKUP-156, 157). None was removed.

**Excel's answers to the 1144 older cases are the third run's, every one**, through COM and typed:
no older case's answer changed. 26 older cases that disagreed in the third run agree now, all
because the corpus or the engine took Excel's observed answer since (ARITH-100, 101, 104, 107, 108,
112, 125, 126, 133, CW-028, FILL-033, FF-023..026, 028, 029, IFERROR-016, ISERROR-010, FMT-076,
077, TYPED-052, 054, XLOOKUP-153..155). Of the third run's disagreements, TABLE-011 and TABLE-013
(both routes), TEXT-103 and TEXT-113 (typed) and COPY-025 (de-DE) remain.

**What stays blocked in every file** (the third run's list, unchanged):

- **TABLE-005..010** (6): a Linked Table still waiting for its data has no Excel counterpart
- **CW-021, CW-022, CW-023, CW-025, CW-026** (5): a width an entry widened the column to
  (`automatic`), which nothing but an entry sets
- Through COM only, and answered typed: the 64 cases the corpus marks "ask by keys" (the third
  run's 58 and FF-030..035); CW-013, CW-014, LVL-023, LVL-024, LVL-030 and NAME-036 (Excel's undo
  does not reach COM's changes) and NAME-006..008 (a qualifier naming no sheet), all nine asked by
  hand in the earlier runs
- With keys only, and answered through COM: 23 XLOOKUP cases with more than 20 cells of their own,
  not typed; TEXT-064 and TEXT-079, whose Formula holds a tab

**Asked by hand: not run.** The nine cases the third run asked with `by-hand.ps1` (NAME-006..008,
NAME-036, CW-013, CW-014, LVL-023, LVL-024, LVL-030) have not changed since the second run, and
this run's procedure names only the oracle's COM and keyboard passes, the formats and `-Update`.

**Reproducibility, and `-Update`.** The `-Update` pass (COM, once per regional format) gave **the
same status and the same answer as the first COM pass for every one of the 1189 cases asked**
(1163 + 17 + 5 + 4). It set `source: "observed"` on the 1048 agreeing cases (1023 + 16 + 5 + 4),
of which **12 changed**: ARITH-136, ARITH-140..145, FMT-078, XLOOKUP-156 and XLOOKUP-157 from
`uncertain`, and FILL-033 and ISERROR-010 from `documented` (both differ from Excel by decision,
and their `excelExpect` now is Excel's answer). Nothing else in the corpus changed. **38 cases stay
`uncertain`**: ARITH-137..139 (they disagree, below), and 35 that COM cannot ask: 25 "ask by
keys" cases (19 added by the third run, and FF-030..035), all of which agree typed; CW-013, CW-014,
LVL-023, LVL-024 and LVL-030, asked by hand in the earlier runs; and CW-021..023, 025, 026. The engine's tests pass on the updated corpus (1764 of 1764).

### The keyboard pass

The third run's `-Keys`, unchanged: each case's own cells and `enter` actions typed into a visible,
maximised Excel, one Unicode character at a time, committed with Enter, with Excel's alerts on while
typing; the dialog watcher answered every dialog with its default button.

**The full keyboard pass took 27 minutes (10:17–10:43). The slowest case took 7.9 seconds, and no
Excel was ended.** As in the second and third runs, **the watcher could not read the text of any
dialog within its two seconds during the full pass**, in all 34 cases that raised one. The same 34
run alone at 10:44 had every text read, with **the same status and the same answer in every case**;
only the wording of the three refusals (TEXT-078, TABLE-011, TABLE-013) differs, as in the third
run. The dialogs were the third run's, in the same 34 cases: the circular-reference warning (26),
"Excel ran out of resources" (TEXT-012, 013, 082), "There's a problem with this formula" (TEXT-078,
TABLE-011, TABLE-013: **refused**), and the typo dialog, whose default makes `=1E308` into
`=E1308` (TEXT-103) and `=-1E308` into `=-E1308` (TEXT-113).

### The format codes typed into Format Cells (FMT-076, 077, 078)

[`format-cells.ps1`](format-cells.ps1), 10:48, under en-US, with real keys: the case's value in A1,
Ctrl+1, Category › Custom (Alt+C, End), the Type box (Alt+T), the code typed over what was there,
then Enter; and while Format Cells was still open, its OK button clicked with the real mouse. A
control, `[Red]0`, went through the same steps first. The steps are in
[`format-cells.jsonl`](format-cells.jsonl) and the screenshots in [`shots/`](shots/) (`A-…`, taken
0.3 s and 2 s after each Enter and each click, Excel's window below its title bar):

| Case | Typed into Type | After Enter | After OK clicked | A1 afterwards |
|---|---|---|---|---|
| control | `[Red]0` | Format Cells closed | — | `[Red]0` |
| FMT-076 | `0;[Color10]-0` | **Format Cells stays open, no message**; the focus is back in Type with the code selected | the same | General, `-3` |
| FMT-077 | `[color3]0` | **the same** | the same | General, `3` |
| FMT-078 | `[Color3]0` | **the same** | the same | General, `3` |

**Typed, Excel does not take any of the three codes, and says nothing**: no message box of any
class was up at 0.3 s or 2 s, and Format Cells was then cancelled with Escape. For FMT-077 and 078
the Sample shows `3` while the code is in the box; for FMT-076 (value -3) it shows nothing
([`A-FMT-077-OK-0.3s.png`](shots/A-FMT-077-OK-0.3s.png)). Through COM the three are refused with
"Unable to set the NumberFormat property of the Range class" (FMT-076, 077 in the third run, FMT-078
now). **The first attempt (10:45) stopped** at the first case on a fault in the script's own
dialog listing, with Format Cells open; it was closed with Escape and the script rewritten. **The
second (10:46)** pressed Enter only and took no screenshot but the dialog's; it saw the same (the
dialog open, no message). Its screenshots are in [`shots/first-attempt/`](shots/first-attempt/),
and all three attempts are in the log. The third (10:48) is the record.

## Disagreements

Each is Excel's answer beside the corpus's expectation. **None was fixed here**, in the engine or in
the case. Where the keyboard's answer differs from COM's, both are given.

### The near-cancel boundary of `=` — ADR-0047, ticket 04

New cases ARITH-136..139 bracket where `=` stops counting `1+x` equal to 1. Through COM and typed
alike:

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| ARITH-136 | `=1+4.2E-15=1` | TRUE (19 units in the last place) | TRUE (agrees) |
| **ARITH-137** | `=1+4.4E-15=1` | FALSE (20 units) | **TRUE** |
| **ARITH-138** | `=1+4.6E-15=1` | FALSE (21 units) | **TRUE** |
| **ARITH-139** | `=1+4.8E-15=1` | FALSE (22 units) | **TRUE** |

Beside them: `=1+5E-15=1` is FALSE in Excel (ARITH-107, the third run and now), so **Excel's
boundary lies between 4.8E-15 and 5E-15** (22 and 23 units in the last place of 1).

The final subtraction's boundary, ARITH-140..142, agrees: `=1+1.3E-15-1` and `=1+1.6E-15-1` are 0,
`=1+1.8E-15-1` is 1.7763568394002505E-15 (2⁻⁴⁹).

### The rest of the new cases: all agree

- **ARITH-143** `=3^0.5` is **1.7320508075688772**, the correctly rounded square root, through COM
  and typed
- **ARITH-144** `="it's">"its"` is TRUE; **ARITH-145** `="ß"<"ss"` is FALSE
- **XLOOKUP-156** `(?<!a)b` finds `b`; **XLOOKUP-157** `(?<=a+)b` is `#VALUE!`
- **FMT-078** `[Color3]0` is refused through COM ("Unable to set the NumberFormat property of the
  Range class"); typed into Format Cells, above
- **FF-030..035**, typed: `=A1+1` over `10%` is `110%` in `0%`; `=1-A1` is `90%` in `0%`; `=A1/2`
  is `0.05`, General; `=A1*2` over `9/26/2026` is `6/23/2153` in `m/d/yyyy`; `=10-50%` is `950.0%`
  in `0.0%`; `=A1-50%` over a date is `46290.5`, General
- **CW-028**, typed: B widens to 10.18, then again to 11.18, `customWidth` set (the corpus took this
  from the third run)

### Linked Tables — ADR-0049, ticket 16

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| TABLE-011 | `=SUM(Positions[Delta])`, no column Delta | `#REF!` | **refused**: COM 0x800A03EC; typed, "There's a problem with this formula" |
| TABLE-013 | `=Positions[PV]`, no table Positions | `#NAME?` | **refused**, as TABLE-011 |

The same in all four runs.

### How a Formula is written back — ADR-0047, ticket 03

| Case | Typed | Engine / corpus | Excel through COM | Excel, typed |
|---|---|---|---|---|
| TEXT-103 | `=1E308` | refused | refused (agrees) | **the typo dialog**, whose default makes it **`=E1308`**, 0 |
| TEXT-113 | `=-1E308` | refused | refused (agrees) | **the typo dialog**, whose default makes it **`=-E1308`**, 0 |

As in the third run.

### Pasting under de-DE — ADR-0048, ticket 14

| Case | Paste | Engine / corpus | Excel |
|---|---|---|---|
| COPY-025, de-DE | the text `1.234,5` then `=SUM(1.5,2)` | 3.5: invariant syntax in every culture | **the text `=SUM(1.5,2)`**, as in the first three runs |

## COM and the keyboard

Every case whose status or answer differs between `results-2026-09-29-run4.json` (COM) and
`results-2026-09-29-run4-keys.json` (keys), as in the third run:

- **Status differs**: the 64 "ask by keys" cases, blocked through COM, **all agree typed** (the
  third run's 50 that agreed, the 8 that disagreed then and agree now — CW-028, FF-023..026, 028,
  029, TYPED-054 — and FF-030..035); TEXT-103 and TEXT-113 agree through COM and disagree typed; and
  the 25 cases the keyboard did not type
- **The same status, another answer**: **the typed entry widened its column, where COM's did not**,
  in 47 cases: the third run's 46 and ARITH-142 (to 11.18). The widths reached were the third
  run's: 8.45 (23 cases), 8.91, 9.27, 10.18 (3), 11.18 (13), 11.82 (3), 13.45 (2) and 13.55. The
  three refusals (TEXT-078, TABLE-011, TABLE-013) are reported by the dialog instead of 0x800A03EC

Under de-DE, ja-JP and en-GB, COM and the keyboard give the same status every time.

## Recorded (the engine differs by decision; Excel's side confirmed)

The same 25 as the second and third runs, with the same answers: ERR-078..081, 083, 084, 093,
NAME-009, NAME-010, STRUCT-021 (a circular reference, ADR-0047 `#CIRC!`); XLOOKUP-048..057 (binary
search over unsorted data, ADR-0047); FILL-045, FILL-052..054 (fill patterns ExSheet does not take,
ADR-0050); REF-006 (`VLOOKUP` outside the declared set, ADR-0047).
