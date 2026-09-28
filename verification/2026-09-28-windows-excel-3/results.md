# Verification — 2026-09-28, Windows, Excel as the oracle, third run

**Scope: Part A of [`verify-on-windows-3.md`](../../docs/specs/exsheet/verify-on-windows-3.md).**
The whole case corpus asked of a real Excel again, through COM and with real keys, with the new
cases among it. Part B, what ADR-0052 still extrapolates and Excel's scroll steps, is in
[`active-cell.md`](active-cell.md). **Parts C and D were not run**: the procedure's gate is not
met (below).

**Verified commit: `20666707a4c8b7b71a899f25ad3e885a02da402c`**, the tip of
`claude/exsheet-start-8cx3v1` when this run began. The results are committed on
`claude/exsheet-windows-verify-3`, branched from that commit. Nothing here changes the engine, an
ADR, `CONTEXT.md` or the Definition of Done. Every disagreement is listed for the user to decide.
The corpus changed only where `-Update` changed it: `source` on the agreeing cases.

## Environment

- The machine of the first two runs: Windows 11 Pro 25H2 (build 26200), one display, 3840×2160
  at 150%, display language en-GB, **regional format en-GB**, system locale ja-JP. The keyboard
  for Excel's window is the Japanese IME (Microsoft IME), kept off (alphanumeric) throughout
- **Excel: Microsoft 365, 16.0.20326.20158, 64-bit**, the build of the first two runs
- Windows PowerShell 5.1 drives Excel. Build and layers 1–2 ran in WSL2 through nix.
  `dotnet build ExGrid.slnx` gave **0 warnings, 0 errors**. `dotnet test ExGrid.slnx` was
  **green**: ExGrid.Tests 724, ExSheet.Engine.Tests 1712, ExGrid.MudBlazor.Tests 75,
  ExSheet.Components.Tests 217 and ExGrid.Components 807 passed, with 1 skipped
  (ExGrid.Components), 0 failed
- **The regional format was changed under the user's advance authorisation** (`Set-Culture`):
  en-US at 09:54 for the whole corpus, then de-DE at 10:34, ja-JP at 10:35 and en-GB at 10:35,
  where the machine was left. `HKCU\Control Panel\International` was exported at 09:54, before
  the first change ([`international-before.reg`](international-before.reg)), and at 10:36, after
  the last ([`international-after.reg`](international-after.reg)). **The two are identical, byte
  for byte** (SHA-256 `071c2b4716c2b1afdad10629c20cc64d99291bf7ec6466fcb1cc8b923456b1af`), and
  identical to the second run's two exports
- The AutoRecovered workbook in `%APPDATA%\Microsoft\Excel` was backed up first and is unchanged

### The two-digit-year setting

Recorded by [`two-digit-year.ps1`](two-digit-year.ps1) into
[`two-digit-year.json`](two-digit-year.json), under en-GB:

- **Windows: `HKCU\Control Panel\International\Calendars\TwoDigitYearMax` does not exist**, so
  the default applies. The Gregorian calendar's `TwoDigitYearMax`, as .NET reads it with the
  user's overrides, is **2049**: a two-digit year reads as 1950–2049. `HKLM` holds only the
  Japanese era table under `Nls\Calendars`
- **Excel's own Options: none found.** File › Options was opened with real keys and every
  category but Customize Ribbon and Quick Access Toolbar (which list commands) was searched
  through UI Automation, 850 texts in all. The one text naming a two-digit year is on Formulas,
  **"Cells containing years represented as 2 digits"**, which is an error-checking rule, not a
  reading of the year
- Typed, Excel follows the Windows window: `1/1/30` is 2030 (DATE-013), `1/1/49` is 2049
  (DATE-027) and `31-Dec-49` is 2049 (DATE-029). Through COM's `FormulaLocal` the corpus notes
  that 30–49 read as 19xx; those cases are asked by keys only

## Part A — the corpus

`tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1`, the whole corpus under en-US, then the cases
that name de-DE, ja-JP and en-GB under those formats, **each through COM and with real keys**, as
in the second run. The script is the one in the verified commit, unchanged.

| File | Regional format | Entered through | Cases | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|---|
| `results-2026-09-28-run3.json` | en-US | COM | 1144 | 995 | 20 | 104 | 25 |
| `results-2026-09-28-run3-keys.json` | en-US | keys | 1144 | 1018 | 30 | 71 | 25 |
| `results-2026-09-28-run3-keys-dialogs.json` | en-US | keys | the 34 that raised a dialog | 19 | 5 | 0 | 10 |
| `results-2026-09-28-run3-de-DE.json` | de-DE | COM | the 17 that name it | 16 | 1 | 0 | 0 |
| `results-2026-09-28-run3-keys-de-DE.json` | de-DE | keys | the 17 that name it | 16 | 1 | 0 | 0 |
| `results-2026-09-28-run3-ja-JP.json` | ja-JP | COM | the 5 that name it | 5 | 0 | 0 | 0 |
| `results-2026-09-28-run3-keys-ja-JP.json` | ja-JP | keys | the 5 that name it | 5 | 0 | 0 | 0 |
| `results-2026-09-28-run3-en-GB.json` | en-GB | COM | the 4 that name it | 3 | 1 | 0 | 0 |
| `results-2026-09-28-run3-keys-en-GB.json` | en-GB | keys | the 4 that name it | 3 | 1 | 0 | 0 |

(The files are in `tests/ExSheet.Engine.Tests/ExcelOracle/`.)

**The corpus has 1144 cases** (the procedure said about 1145), **88 more than the second run's
1056**, every new one `uncertain`. None was removed. 17 older cases are still `uncertain`; all
of them are asked by keys or by hand, or are blocked.

**Excel's answers to the 1056 older cases are the second run's, every one**: through COM and
typed, no older case's answer changed, and no older case disagrees now where it did not then. The
older cases whose status changed did so because the corpus changed (it took Excel's observed
answers, or moved a case to "ask by keys"). Of the second run's disagreements, only FILL-033,
ISERROR-010, TABLE-011 and TABLE-013 (both routes), TEXT-103 (typed) and COPY-025 (de-DE) remain.

**The 88 new cases.** Through COM: 50 agree, 16 disagree, 22 blocked (20 are asked by keys only;
TYPED-050 and TYPED-051 name another format). Typed: 61 agree, 25 disagree, 2 blocked (the two
naming another format, answered in their own files).

**What stays blocked in every file:**

- **TABLE-005..010** (6): a Linked Table still waiting for its data has no Excel counterpart
- **CW-021, CW-022, CW-023, CW-025, CW-026** (5): a width an entry widened the column to
  (`automatic`), which nothing but an entry sets
- Through COM only, and answered below: the 58 cases the corpus marks "ask by keys"; NAME-006..008,
  NAME-036, CW-013, CW-014, LVL-023, LVL-024 and LVL-030 (asked by hand)
- With keys only, and answered through COM: 23 XLOOKUP cases with more than 20 cells of their own
  (XLOOKUP-110, XLOOKUP-123..144), not typed; TEXT-064 and TEXT-079, whose Formula holds a tab

**Reproducibility, and `-Update`.** The `-Update` pass is a second COM run, once per regional
format. It gave **the same status and the same answer as the first COM run for every one of the
1170 cases asked** (1144 + 17 + 5 + 4). It then set `source: "observed"` on the 1019 agreeing
cases (995 + 16 + 5 + 3), of which **51 changed, all from `uncertain`**: 50 new cases under
en-US and TYPED-050 under de-DE. Nothing else in the corpus changed. The engine's tests still pass
on the updated corpus (1712 of 1712). `-Update` rests on COM's answers: **TEXT-113**, one of the
51, is a case the keyboard contradicts (below).

### The keyboard pass

The second run's `-Keys`, unchanged: each case's own cells and `enter` actions typed into a
visible, maximised Excel, one Unicode character at a time, committed with Enter, with Excel's
alerts on while typing. The dialog watcher answered every dialog with its default button.

**The full keyboard pass took 26 minutes (09:59–10:25). The slowest case took 8.0 seconds, and
no Excel was ended.** As in the second run, **the watcher could not read the text of any dialog
within its two seconds during the full pass**, in all 34 cases that raised one. The same 34 run
alone at 10:26 had every text read, with **the same status and the same answer in every case**;
only the wording of the three refusals differs, which in the full pass says "after the dialog it
raised" and in the rerun quotes the dialog. The dialogs' texts below come from the rerun.

**The dialogs the keyboard met**, in 34 cases, all answered with their default button:

| Dialog | Cases | Outcome |
|---|---|---|
| "There are one or more circular references where a formula refers to its own cell either directly or indirectly…" (Warning, OK) | ERR-078..093, IF-015, IFERROR-010, ISERROR-010, NAME-009, NAME-010, STRUCT-021..023, TYPED-019, TYPED-020 (26) | the entry stands |
| "Excel ran out of resources while attempting to calculate one or more formulas…" (Error, OK) | TEXT-012, TEXT-013, TEXT-082 | the entry stands |
| "There's a problem with this formula. Not trying to type a formula? …" (Warning, OK) | TEXT-078, TABLE-011, TABLE-013 | Excel goes back to the edit; Escape cancels it: **refused** |
| "We found a typo in your formula and tried to correct it to: =E1308. Do you want to accept this correction?" (Information, Yes/No) | TEXT-103 (`=1E308`) | Yes, the default: **`=E1308`**, 0 |
| "We found a typo in your formula and tried to correct it to: =-E1308…" | **TEXT-113** (`=-1E308`), new | Yes, the default: **`=-E1308`**, 0 |

## Disagreements

Each is Excel's answer beside the corpus's expectation. **None was fixed here**, in the engine or
in the case. Where the keyboard's answer differs from COM's, both are given. Every one is listed
under the ADR its corpus area names and the ticket that area belongs to.

### The near-cancel boundary: a final `+`/`-`, and `=` — ADR-0047, ticket 04

New cases ARITH-099..121 bracket where Excel sets a final subtraction to 0 and where `=` counts
two numbers as equal. Through COM and typed alike:

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| ARITH-100 | `=1+5E-16-1` | 4.440892098500626E-16 (2⁻⁵¹): the threshold is strict | **0** |
| ARITH-101 | `=1+1E-15-1` | 1.1102230246251565E-15 | **0** |
| ARITH-104 | `=1.1-1-0.1` | 8.326672684688674E-17 | **0** |
| ARITH-107 | `=1+5E-15=1` | TRUE: 23 units in the last place apart are equal | **FALSE** |
| ARITH-108 | `=1+6E-15=1` | TRUE | **FALSE** |
| ARITH-112 | `=1+5E-15<>1` | FALSE | **TRUE** |

Beside them, agreeing: `=1+2E-16-1` is 0 (ARITH-099), `=1+2E-15-1` is 1.9984014443252818E-15
(ARITH-102) and `=1+2.5E-15-1` is 2.4424906541753444E-15 (ARITH-103); `=1+1E-15=1` is TRUE
(ARITH-106) and `=1+7E-15=1`, `8E-15`, `9E-15` are FALSE (ARITH-109..111). ARITH-113..121
(SUM, parentheses, a unary minus, `+0`) agree.

### Collation and powers — ADR-0047, ticket 04

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| ARITH-125 | `="it's"<"its"` | TRUE: an apostrophe sorts before a letter | **FALSE** |
| ARITH-126 | `="ß"="ss"` | FALSE | **TRUE** |
| ARITH-133 | `=2^0.5` | 1.414213562373095 | **1.4142135623730951** |

ARITH-122..124 and 127..128 (hyphens, `ß` against `sr` and `st`) and ARITH-134, 135 agree.

### IFERROR with the value_if_error left out — ADR-0047, ticket 04

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| IFERROR-016 | `=IFERROR(1/0,)&""` | `"0"`: a missing value_if_error is the number 0 | **`""`** |

### XLOOKUP's regular expressions (match_mode 3) — ADR-0047, ticket 04

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| XLOOKUP-153 | `=XLOOKUP("(?<=a)b",B1:B2,C1:C2,,3)` over `ab`, `b` | `#VALUE!`: a lookbehind is refused | **`a`** |
| XLOOKUP-154 | `=XLOOKUP("\p{Greek}",…,3)` over `α`, `a` | `#VALUE!`: a script is refused | **`a`** |
| XLOOKUP-155 | `=XLOOKUP("^\w$",…,3)` over `𝐀` (outside the BMP), `a` | `#VALUE!` | **`a`** |

### Number formats — ADR-0047, ticket 05

| Case | Format | Engine / corpus | Excel |
|---|---|---|---|
| FMT-076 | `0;[Color10]-0` | accepted, as a named colour is | **refused**: "Unable to set the NumberFormat property of the Range class" |
| FMT-077 | `[color3]0` | accepted | **refused**, the same |

### Typed constants — ADR-0048, ticket 05

| Case | Typed | Engine / corpus | Excel through COM | Excel, typed |
|---|---|---|---|---|
| TYPED-052 | `$-5` | text | **the number -5**, format `$#,##0_);[Red]($#,##0)`, shows `($5)` | the same |
| TYPED-054 | `-B2 C2` | text: ExSheet does not read an intersection | (asked by keys only) | **the Formula `=-B2 C2`, `#NULL!`** |
| TYPED-051, en-GB | `5-Oct` | shows `5-Oct` | **shows `05-Oct`** (46300, format `d-mmm`) | the same |

TYPED-050 under de-DE (`5-Okt` shows `05. Okt`) and TYPED-053 (`-total B2` is `=-total B2`,
`#NAME?`) agree.

### A Formula's result format — ADR-0047, ticket 05

Asked by keys only (through COM Excel gives these results no format):

| Case | Setup | Engine / corpus | Excel, typed |
|---|---|---|---|
| FF-023 | `=A1/2` over a typed date | General | **`m/d/yyyy`**, `5/14/1963` |
| FF-024 | `=50%` | `0.0%`, `50.0%` | **General, `0.5`** |
| FF-025 | `=A1*2` over a typed `10%` | `0%`, `20%` | **General, `0.2`** |
| FF-026 | `=SUM(A1,5)` over a typed date | General | **`m/d/yyyy`**, `10/1/2026` |
| FF-028 | `=2*50%` | `0.0%`, `100.0%` | **General, `1`** |
| FF-029 | `=A1+50%` over a typed date | `m/d/yyyy` | **General, `46291.5`** |

FF-027 (`=A1%`, General) agrees. In the second run a typed `=10+50%` (ARITH-006) took `0.0%`;
it does again here (ARITH-006 agrees).

### Column widths — ADR-0046 and ADR-0047, ticket 05

Asked by keys only:

| Case | Typed | Engine / corpus | Excel, typed |
|---|---|---|---|
| CW-028 | `1234567890` in B2, then `12345678901` in B3 | B is not widened again: at most 10.5 | **widened again, to 11.18**; `customWidth` set |

CW-027 (a typed date widens B to 8.45 and sets `customWidth`) and CW-029 (a typed Formula whose
number widens B to 10.18 sets it) agree.

### How a Formula is written back — ADR-0047, ticket 03

| Case | Typed | Engine / corpus | Excel through COM | Excel, typed |
|---|---|---|---|---|
| TEXT-103 | `=1E308` | refused | refused (0x800A03EC; agrees) | **the typo dialog**, whose default makes it **`=E1308`**, 0 |
| TEXT-113 | `=-1E308` | refused | refused (agrees) | **the typo dialog**, whose default makes it **`=-E1308`**, 0 |

TEXT-103's corpus text already says Excel offers the correction typed and that ExSheet does not
correct a Formula into another; the oracle still counts the typed answer as a disagreement.
TEXT-113 is new, and `-Update` marked it `observed` on COM's answer.

### Linked Tables — ADR-0049, ticket 16

| Case | Formula | Engine / corpus | Excel |
|---|---|---|---|
| TABLE-011 | `=SUM(Positions[Delta])`, no column Delta | `#REF!` | **refused**: COM 0x800A03EC; typed, "There's a problem with this formula" |
| TABLE-013 | `=Positions[PV]`, no table Positions | `#NAME?` | **refused**, as TABLE-011 |

The same in all three runs.

### Pasting under de-DE — ADR-0048, ticket 14

| Case | Paste | Engine / corpus | Excel |
|---|---|---|---|
| COPY-025, de-DE | the text `1.234,5` then `=SUM(1.5,2)` | 3.5: invariant syntax in every culture | **the text `=SUM(1.5,2)`**, as in the first two runs |

### Where the engine differs by decision, and the corpus's `excelExpect` is not what Excel does

The same two as the first two runs, with the same answers:

| Case | Decision | Corpus's `excelExpect` | Excel |
|---|---|---|---|
| ISERROR-010 | ADR-0047, `#CIRC!` | FALSE | **0**: a Formula depending on a cycle is left uncalculated |
| FILL-033 | ADR-0050, a date with a time is refused | 46293.416666666664 | **46293.4166666088** |

## COM and the keyboard

Every case whose status or answer differs between `results-2026-09-28-run3.json` (COM) and
`results-2026-09-28-run3-keys.json` (keys). The 25 cases the keyboard did not type are left out.

**Status differs:**

| Case | COM | Keys | What differs |
|---|---|---|---|
| The 58 "ask by keys" cases | blocked | 50 agree, **8 disagree** | the 8: CW-028, FF-023..026, FF-028, FF-029, TYPED-054 (above) |
| TEXT-103, TEXT-113 | agree | **disagree** | refused through COM; typed, corrected by the typo dialog |

The 50 that agree typed include the second run's keyboard-only answers, now in the corpus:
DATE-013 (2030), ERR-096..106 (typed newer Error Values), LVL-015 (automatic percent entry),
FF-011, FF-013, CW-018, WD-001..013, TYPED-019, 020, 028..032, TEXT-077. New among them: DATE-027,
DATE-029, ERR-107 (`=IFERROR(A1,"caught")` over a typed `#SPILL!` is `caught`), ERR-108
(`=A1+1` over a typed `#CALC!` is `#CALC!`), LVL-031..033 and LVL-035 (a number typed into a
percent cell is divided by 100: 10 is 10%, 50% stays 50%, 0.5 in `0.00%` is 0.50%, -5 is -5%),
FF-027, CW-027, CW-029, TYPED-053.

**The same status, another answer:** **the typed entry widened its column, where COM's did not**,
in 46 cases, as in the second run: the second run's 39 less CW-018 (now asked by keys only), and
the new ARITH-102, 103, 116, 117, 120, 121, TEXT-107 and TEXT-108 (to 10.18, 11.18 or 11.82). The
widths reached were those of the second run: 8.45 (a date), 8.91, 9.27, 10.18, 11.18, 11.82, 13.45
and 13.55. None of these cases states a width, so the status is the same. The three refusals (TEXT-078, TABLE-011, TABLE-013) are reported by the
dialog instead of 0x800A03EC.

Under de-DE, ja-JP and en-GB, COM and the keyboard give the same status every time, and differ only
in the typed dates widening their column (DATE-006..011, TYPED-027, TYPED-044, TYPED-045).

## Asked by hand

[`by-hand.ps1`](by-hand.ps1), the second run's script copied unchanged but for its paths, asked
the nine cases the oracle cannot ask through COM (10:27–10:28), with Excel's own UI and real keys
and mouse. The cases are unchanged since the second run. Its steps are in
[`by-hand.jsonl`](by-hand.jsonl) and its screenshots in [`shots/`](shots/); the three of the file
dialog are cut down to its title bar, since the dialog opens on the user's own Documents folder.
**All nine agree with the corpus, with the second run's answers**: NAME-006..008 raise the file
dialog "Update Values: …", and after Escape the Formula stays and shows `#REF!`; NAME-036, CW-013,
CW-014, LVL-023, LVL-024 and LVL-030 undo as the corpus says.

## Recorded (the engine differs by decision; Excel's side confirmed)

The same 25 as the second run, with the same answers: ERR-078..081, 083, 084, 093, NAME-009,
NAME-010, STRUCT-021 (a circular reference, ADR-0047 `#CIRC!`); XLOOKUP-048..057 (binary search
over unsorted data, ADR-0047); FILL-045, FILL-052..054 (fill patterns ExSheet does not take,
ADR-0050); REF-006 (`VLOOKUP` outside the declared set, ADR-0047).

## Parts C and D: not run

The procedure starts Parts C and D only when `origin/claude/exsheet-start-8cx3v1` holds all five
gate items. Checked at 09:53 on `2066670` and again at 10:41 on `7db2677`:

| Item | Present | Where |
|---|---|---|
| 1. ADR-0052 implemented, no `test.fail(true, 'ADR-0052` left in `sheet-vs-excel.spec.mjs` | yes | `67b5f81` merges it; 0 markers left |
| 2. ADR-0053, with a `chrome-150` project in `playwright.config.mjs` | yes | `7f5fefa`; the config names `chrome-150` |
| 3. `GridPasteIntent.Refuse` | yes | `48cf90d` |
| 4. The Server Formula Bar fix (DC-19/DC-22) | yes | `0efc1fe`, `a07788a` |
| 5. `main` merged in (`git merge-base --is-ancestor b894b69 origin/claude/exsheet-start-8cx3v1`) | **no** | fails at both tips |

**Item 5 is missing, so Parts C and D were not run**, and A and B are pushed, as the procedure
says. The narrowed focus hand-back (ADR-0021, "Widened 2026-09-28") is on the branch from
`1f7d4e0`, before the verified commit.
