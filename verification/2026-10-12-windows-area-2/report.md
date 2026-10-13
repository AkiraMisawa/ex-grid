# The second area pass, 2026-10-12

**Verified commit:** `afd70b156e40961dfbd505ae9fbd84c748423ec6` — tagged `verified/2026-10-12-windows-area-2`

A second `oracle.ps1 -Area` pass, asked for by the session preparing `0.1.0-rc.1`. It puts to Excel the 160
`uncertain` cases the first pass's corrections added (branch `claude/area-corrections`), so that Excel's doubles decide
between the evaluations its first answers left open. It follows `docs/specs/exsheet/verify-on-windows-17.md`, Part A,
steps 1 to 5, over a smaller scope. **Nothing was decided.** The disagreements are listed for a human, as `oracle.ps1`
says. `-Update` rewrote `source` to `observed` only for the cases that agreed in the same call.

- **Commit asked:** `afd70b15` (`claude/area-corrections`: the first pass's corrections, merged with `main` at
  `a533de01`), on branch `claude/exsheet-windows-area-2026-10-12b`.
- **The oracle:** `oracle.ps1` as `afd70b15` has it, which writes each double's `number` with `G17` instead of `R`.
  **Every double below is Excel's `number`.** Over the 360 numeric answers of the pass, `number` and `value2` (which
  `ConvertTo-Json` wrote) name the same double in every case.
- **Machine:** the seventeenth run's (Windows 11 Pro 25H2, build 26200). Excel 16.0.20430.20146 x64 (`EXCEL.EXE
  16.0.20430.20146`), English (UK) user interface.
- **Regional format:** en-GB before the pass. Set to en-US at 16:13:23 for the COM pass, put back to en-GB at 16:15:13.
  `HKCU\Control Panel\International` exported before and after reads the same (SHA-256 `071c2b47…456b1af`,
  `logs/restore-culture.log`). The user had said a change of regional format need not be asked each time.
- **Files** (25): `aggregate, day, days, edate, eomonth, fv, indirect, large, linked-tables, mod, month, networkdays, npv,
  pmt, pv, sheet-names, small, sort, sortby, text, unique, weekday, workday, xnpv, year`. They hold every `uncertain`
  case on `afd70b15` (160, in 23 files) and the 10 `documented` ones (`linked-tables`, `sheet-names`).
- **One pass:** `oracle.ps1 -Area <the 25> -Update` through COM under en-US, 16:13:24 to 16:15:00. No case asked was
  blocked with "ask by keys", so step 3 had nothing to ask. The only cases naming a culture are TEXTFN-008 (ja-JP),
  TEXTFN-009 and TEXTFN-010 (de-DE), already `observed` (the first pass asked them under their cultures), so step 4 was
  not repeated for them.

## Counts

Before (`afd70b15`) and after the pass, the 25 files (`counts-before.md`, `counts-after.md`; `count.py` wrote both):

| | cases | observed | documented | uncertain |
|---|---|---|---|---|
| before | 526 | 356 | 10 | 160 |
| after | 526 | 422 | 10 | 94 |

- `-Update` rewrote `source` on 66 cases, each `uncertain` → `observed`. `git diff` touches no other line.
- **The whole corpus after the pass:** observed 3117, documented 10, uncertain 94. Nothing outside the 25 files is not
  `observed`.

## The pass

| Results file (`tests/ExSheet.Engine.Tests/ExcelOracle/`) | Regional format | How | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|
| `results-2026-10-12-area-2-com-en-US.json` | en-US | COM, the 25 files, `-Update` | 419 | 63 | 13 | 31 |

The log is `logs/com-en-US.log`, and the scripts that ran it and put the format back are in `scripts/`. `summarise.py`
reads the results and the corpus before and after, and writes `summary.md`, which lists every case of the 25 files once.

## Every disagreement (63)

Excel's double is `number`. "Units in the last place" counts the doubles between Excel's and the expected one.

| Case | File | Formula | Excel | Excel's text | Expected | Units in the last place |
|---|---|---|---|---|---|---|
| EDATE-016 | `edate.json` | `=EDATE(0,1)` | 31 | `31` | `#VALUE!` | |
| EDATE-017 | `edate.json` | `=EDATE(-0.5,1)` | `#NUM!` | `#NUM!` | `#VALUE!` | |
| EOMONTH-016 | `eomonth.json` | `=EOMONTH(-0.5,0)` | `#NUM!` | `#NUM!` | `#VALUE!` | |
| EOMONTH-017 | `eomonth.json` | `=EOMONTH(-0.5,1)` | `#NUM!` | `#NUM!` | `#VALUE!` | |
| FV-014 | `fv.json` | `=FV(0.05/12,12,-150,-1000,1)` | 2900.664506306397 | `$2,900.66 ` | `#VALUE!` | |
| FV-015 | `fv.json` | `=FV(0.03/12,240,-100,-50000,1)` | 123950.02507801351 | `$123,950.03 ` | `#VALUE!` | |
| FV-016 | `fv.json` | `=FV(0.42,0.5,-100)` | 45.627983043166275 | `$45.63 ` | `#VALUE!` | |
| FV-017 | `fv.json` | `=FV(-9,1/3,-1)` | 0.33333333333333331 | `$0.33 ` | `#VALUE!` | |
| FV-018 | `fv.json` | `=FV(-1,0,-100,-500)` | `#NUM!` | `#NUM!` | 500 | |
| FV-020 | `fv.json` | `=FV(-1,-0.5,-100)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` | |
| FV-021 | `fv.json` | `=FV(-1,-10,-100)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` | |
| FV-022 | `fv.json` | `=FV(0.06/12,-2.5,-100)` | -247.8287941856272 | `($247.83)` | `#VALUE!` | |
| MOD-020 | `mod.json` | `=MOD(1,0.1)` | 0.09999999999999995 | `0.1` | `#VALUE!` | |
| MOD-021 | `mod.json` | `=MOD(5,0.1)` | 0.099999999999999728 | `0.1` | `#VALUE!` | |
| MOD-022 | `mod.json` | `=MOD(-2.2,0.1)` | 0.09999999999999995 | `0.1` | `#VALUE!` | |
| MOD-023 | `mod.json` | `=MOD(-1,-0.1)` | -0.09999999999999995 | `-0.1` | `#VALUE!` | |
| MOD-024 | `mod.json` | `=MOD(3.3,0.01)` | 0.0099999999999997539 | `0.01` | `#VALUE!` | |
| MOD-025 | `mod.json` | `=MOD(0.7,0.01)` | 0.0099999999999999412 | `0.01` | `#VALUE!` | |
| MOD-026 | `mod.json` | `=MOD(100000000000,0.1)` | 0.09999444888487688 | `0.099994449` | `#VALUE!` | |
| MOD-027 | `mod.json` | `=MOD(123456789012,0.2)` | 0.19999314677151464 | `0.199993147` | `#VALUE!` | |
| MOD-028 | `mod.json` | `=MOD(-693380,-93.7)` | -93.699999999978971 | `-93.7` | `#VALUE!` | |
| MOD-029 | `mod.json` | `=MOD(-0.087626,6630)` | 6629.9123739999995 | `6629.912374` | `#VALUE!` | |
| MOD-030 | `mod.json` | `=MOD(1.1,0.1)` | 2.7755575615628914E-17 | `2.77556E-17` | `#VALUE!` | |
| MOD-031 | `mod.json` | `=MOD(-0.9,0.1)` | 2.7755575615628914E-17 | `2.77556E-17` | `#VALUE!` | |
| MOD-032 | `mod.json` | `=MOD(-1E-20,1)` | 0 | `0` | `#VALUE!` | |
| PMT-013 | `pmt.json` | `=PMT(0.05/12,36,250000,-5000)` | -7363.7031239763755 | `($7,363.70)` | -7363.703123976373 | 3 (2.7e-12) |
| PMT-014 | `pmt.json` | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725485 | `($21,530.43)` | -21530.430018725492 | 2 (7.3e-12) |
| PMT-015 | `pmt.json` | `=PMT(0.05/12,12,10000,0,1)` | -852.52264021710005 | `($852.52)` | -852.5226402170998 | 2 (2.3e-13) |
| PMT-016 | `pmt.json` | `=PMT(0.045/12,12,250000,-5000)` | -20936.487788661154 | `($20,936.49)` | -20936.48778866115 | 1 (3.6e-12) |
| PMT-018 | `pmt.json` | `=PMT(0.0365/365,365,10000)` | -27.901671561224465 | `($27.90)` | -27.90167156122446 | 1 (3.6e-15) |
| PMT-020 | `pmt.json` | `=PMT(0.15,12,1000,-5000,1)` | -10.501648240558216 | `($10.50)` | -10.501648240558218 | 1 (1.8e-15) |
| PMT-021 | `pmt.json` | `=PMT(0.15,60,250000,-5000,1)` | -32615.986669943602 | `($32,615.99)` | -32615.986669943606 | 1 (3.6e-12) |
| PMT-023 | `pmt.json` | `=PMT(0.0365/365,24,250000,20000,1)` | -11260.941796175353 | `($11,260.94)` | -11260.941796175352 | 1 (1.8e-12) |
| PMT-024 | `pmt.json` | `=PMT(0.0525/12,24,250000,-5000,1)` | -10750.7823801456 | `($10,750.78)` | -10750.782380145603 | 2 (3.6e-12) |
| PMT-025 | `pmt.json` | `=PMT(0.065/12,60,1000,-5000,1)` | 50.905521301199329 | `$50.91 ` | 50.90552130119935 | 3 (2.1e-14) |
| PMT-026 | `pmt.json` | `=PMT(0.07/12,240,1000,-5000)` | 1.8452907580883169 | `$1.85 ` | 1.8452907580883144 | 11 (2.4e-15) |
| PMT-027 | `pmt.json` | `=PMT(0.00001,360,10000,-5000,1)` | -13.963833694802407 | `($13.96)` | -13.963833694802405 | 1 (1.8e-15) |
| PMT-028 | `pmt.json` | `=PMT(0.025/4,240,1000,-5000)` | 0.97372774207228763 | `$0.97 ` | 0.9737277420722884 | 7 (7.8e-16) |
| PMT-029 | `pmt.json` | `=PMT(0.06/52,60,1000,-5000,1)` | 63.197272167756296 | `$63.20 ` | 63.197272167756275 | 3 (2.1e-14) |
| PMT-030 | `pmt.json` | `=PMT(0.045/12,12,10000,-5000)` | -445.64260793186031 | `($445.64)` | -445.64260793186025 | 1 (5.7e-14) |
| PMT-031 | `pmt.json` | `=PMT(0.001,12,250000,20000,1)` | -22603.914076303507 | `($22,603.91)` | -22603.91407630351 | 1 (3.6e-12) |
| PMT-032 | `pmt.json` | `=PMT(0.03/12,12,250000,20000)` | -22817.298664792012 | `($22,817.30)` | -22817.298664792015 | 1 (3.6e-12) |
| PV-011 | `pv.json` | `=PV(-1.5,0.5,-100)` | `#NUM!` | `#NUM!` | `#VALUE!` | |
| PV-012 | `pv.json` | `=PV(-1,-10,-100)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` | |
| SMALL-008 | `small.json` | `=SMALL(A1:A9,9.5)` | `#NUM!` | `#NUM!` | 7 | |
| SORT-015 | `sort.json` | `=SORT(A1:A2)` | 7 | `7` | `#VALUE!` | |
| SORT-021 | `sort.json` | `=COUNTA(SORT(Q1:Q2))` | 2 | `2` | `#VALUE!` | |
| SORT-022 | `sort.json` | `=ISBLANK(INDEX(SORT(Q1:Q2),2))` | TRUE | `TRUE` | `#VALUE!` | |
| UNIQUE-017 | `unique.json` | `=COUNTA(UNIQUE(Q1:Q2))` | 2 | `2` | `#VALUE!` | |
| UNIQUE-018 | `unique.json` | `=INDEX(UNIQUE(Q1:Q2),2)=""` | TRUE | `TRUE` | `#VALUE!` | |
| UNIQUE-019 | `unique.json` | `=SUM(--(LEN(UNIQUE(A1:A3))>0))` | 2 | `2` | `#VALUE!` | |
| XNPV-005 | `xnpv.json` | `=XNPV(0.07,A1:A4,B1:B4)` | 2023.0065393622904 | `2023.006539` | `#VALUE!` | |
| XNPV-006 | `xnpv.json` | `=XNPV(0.06,A1:A4,B1:B4)` | 2277.5848519247406 | `2277.584852` | `#VALUE!` | |
| XNPV-007 | `xnpv.json` | `=XNPV(-1,A1:A3,B1:B3)` | `#NUM!` | `#NUM!` | `#VALUE!` | |
| XNPV-008 | `xnpv.json` | `=XNPV(-1,A1:A2,B1:B2)` | `#NUM!` | `#NUM!` | `#VALUE!` | |
| XNPV-009 | `xnpv.json` | `=XNPV(-1.5,A1:A3,B1:B3)` | `#NUM!` | `#NUM!` | `#VALUE!` | |
| XNPV-010 | `xnpv.json` | `=XNPV(-2,A1:A2,B1:B2)` | `#NUM!` | `#NUM!` | `#VALUE!` | |
| XNPV-011 | `xnpv.json` | `=XNPV(0.05,A1:A2,B1:B2)` | -7286.8778090094384 | `-7286.877809` | `#VALUE!` | |
| XNPV-012 | `xnpv.json` | `=XNPV(-0.5,A1:A7,B1:B7)` | `#NUM!` | `#NUM!` | `#VALUE!` | |
| XNPV-013 | `xnpv.json` | `=XNPV(0.1,A1:A6,B1:B6)` | 700.22697700285767 | `700.226977` | 700.2269770028581 | 4 (4.5e-13) |
| XNPV-014 | `xnpv.json` | `=XNPV(0.06,A1:A5,B1:B5)` | 5895.8664861795514 | `5895.866486` | `#VALUE!` | |
| XNPV-015 | `xnpv.json` | `=XNPV(0.07,A1:A7,B1:B7)` | 4384.9427145314748 | `4384.942715` | `#VALUE!` | |
| XNPV-017 | `xnpv.json` | `=XNPV(-0.5,A1:A7,B1:B7)` | `#NUM!` | `#NUM!` | `#VALUE!` | |

They fall into three kinds. This describes what Excel answered; it is not a decision:

- **The engine refuses with `#VALUE!` where Excel answers a number or a truth value (30):** EDATE-016; FV-014 to FV-017
  and FV-022; MOD-020 to MOD-032; SORT-015, SORT-021, SORT-022; UNIQUE-017 to UNIQUE-019; XNPV-005, XNPV-006, XNPV-011,
  XNPV-014, XNPV-015.
- **Another error, or an error where the engine answers (15):**
  - Excel answers `#NUM!` where the engine expects `#VALUE!`: EDATE-017, EOMONTH-016, EOMONTH-017, PV-011, XNPV-007 to
    XNPV-010, XNPV-012, XNPV-017.
  - Excel answers `#DIV/0!` where the engine expects `#VALUE!`: FV-020, FV-021, PV-012.
  - Excel answers `#NUM!` where the engine answers a number: FV-018 (500), SMALL-008 (7).
- **The number differs in its last digits (18):** PMT-013 to PMT-016, PMT-018, PMT-020, PMT-021, PMT-023 to PMT-032,
  by 1 to 11 units in the last place, and XNPV-013 by 4. In each, the text Excel shows (in the currency or General
  format it gave the cell) is the text the expected number would show.

## Recorded: the engine refuses by decision, Excel's answer is read and not compared (31)

| Case | File | Formula | Excel | Excel's text | The engine |
|---|---|---|---|---|---|
| INDIRECT-044 | `indirect.json` | `=INDIRECT("RC[-1]",FALSE)` | 1.333 | `1.333` | `#VALUE!` |
| INDIRECT-051 | `indirect.json` | `=SUM(INDIRECT("RC[-1]:R[1]C[-1]",FALSE))` | 46.332999999999998 | `46.333` | `#VALUE!` |
| INDIRECT-052 | `indirect.json` | `=INDIRECT("R[-5]C[-1]",FALSE)` | 0 | `0` | `#VALUE!` |
| INDIRECT-053 | `indirect.json` | `=COLUMNS(INDIRECT("Positions"))` | 2 | `2` | `#VALUE!` |
| INDIRECT-054 | `indirect.json` | `=ROWS(INDIRECT("Positions"))` | 2 | `2` | `#VALUE!` |
| INDIRECT-055 | `indirect.json` | `=INDEX(INDIRECT("Positions"),1,1)` | `R-1` (text) | `R-1` | `#VALUE!` |
| INDIRECT-056 | `indirect.json` | `=COUNTA(INDIRECT("Positions"))` | 4 | `4` | 1 |
| INDIRECT-057 | `indirect.json` | AAA1000, spilled by `=INDIRECT("Positions")` in ZZ1000 | 250.5 | `250.5` | an empty cell |
| INDIRECT-058 | `indirect.json` | `=SUM(INDIRECT("R2C2:R3",FALSE))` | `#REF!` | `#REF!` | `#VALUE!` |
| INDIRECT-059 | `indirect.json` | `=INDIRECT("R03C2",FALSE)` | 45 | `45` | `#VALUE!` |
| INDIRECT-060 | `indirect.json` | `=SUM(INDIRECT("R1C1#",FALSE))` | 6 | `6` | `#VALUE!` |
| INDIRECT-061 | `indirect.json` | `=INDIRECT("Positions[PV]",FALSE)` | 250.5 | `250.5` | `#VALUE!` |
| INDIRECT-062 | `indirect.json` | `=ROWS(INDIRECT("R3:C2",FALSE))` | `#REF!` | `#REF!` | `#VALUE!` |
| INDIRECT-063 | `indirect.json` | `=INDIRECT("R[+1]C[-1]",FALSE)` | 45 | `45` | `#VALUE!` |
| INDIRECT-064 | `indirect.json` | `=INDIRECT("R[0]C[-1]",FALSE)` | 1.333 | `1.333` | `#VALUE!` |
| INDIRECT-065 | `indirect.json` | `=INDIRECT("R[-0]C[-1]",FALSE)` | 1.333 | `1.333` | `#VALUE!` |
| INDIRECT-066 | `indirect.json` | `=INDIRECT("R[2]C",FALSE)` | 45 | `45` | `#VALUE!` |
| NPV-009 | `npv.json` | `=NPV(0.12,-25000,6000,8000,9000,7500,4000)` | 157.86528462709433 | `$157.87 ` | `#NAME?` |
| NPV-010 | `npv.json` | `=NPV(0.045,-10000,2000,2000,2000,2000,2000,2000,2000)` | 1708.5185462007141 | `$1,708.52 ` | `#NAME?` |
| NPV-011 | `npv.json` | `=NPV(0.045,100,200,300,400,500,600,700,800,900,1000)` | 4065.5859439548863 | `$4,065.59 ` | `#NAME?` |
| NPV-012 | `npv.json` | `=NPV(0.05,-25000,6000,8000,9000,7500,4000)` | 4808.9839576631866 | `$4,808.98 ` | `#NAME?` |
| NPV-013 | `npv.json` | `=NPV(0.08,-120000,39000,30000,21000,37000,46000)` | 15745.078146860713 | `$15,745.08 ` | `#NAME?` |
| NPV-014 | `npv.json` | `=NPV(0.09,-10000,2000,2000,2000,2000,2000,2000,2000)` | 60.463917567430045 | `$60.46 ` | `#NAME?` |
| NPV-015 | `npv.json` | `=NPV(0.1,-10000,2000,2000,2000,2000,2000,2000,2000)` | -239.2385132855801 | `($239.24)` | `#NAME?` |
| NPV-016 | `npv.json` | `=NPV(0.1,-25000,6000,8000,9000,7500,4000)` | 1303.8501073347138 | `$1,303.85 ` | `#NAME?` |
| NPV-017 | `npv.json` | `=NPV(0.07,-10000,2000,2000,2000,2000,2000,2000,2000)` | 727.64374139943288 | `$727.64 ` | `#NAME?` |
| NPV-018 | `npv.json` | `=NPV(0.07,-25000,6000,8000,9000,7500,4000)` | 3285.3515532789606 | `$3,285.35 ` | `#NAME?` |
| NPV-019 | `npv.json` | `=NPV(0.045,-120000,39000,30000,21000,37000,46000)` | 29793.511231516124 | `$29,793.51 ` | `#NAME?` |
| NPV-020 | `npv.json` | `=NPV(0.08,-7500,3000,3000,3000)` | 214.15829791077385 | `$214.16 ` | `#NAME?` |
| NPV-021 | `npv.json` | `=NPV(0.09,-40000,8000,9200,10000,12000,14500)` | 669.58524225299175 | `$669.59 ` | `#NAME?` |
| NPV-022 | `npv.json` | `=NPV(0.15,-25000,6000,8000,9000,7500,4000)` | -1338.223462825282 | `($1,338.22)` | `#NAME?` |

- The decisions: INDIRECT-044, 051, 052 and 058 to 066 cite ADR-0124's note of 2026-10-12 (R1C1 text in a form no run
  has observed, and an offset past the Sheet's edge). INDIRECT-053 to 057 cite ADR-0124's notes of 2026-10-10 and
  2026-10-12 (a declared Linked Table's bare name). NPV-009 to NPV-022 cite ADR-0202's note of 2026-10-12 (NPV is
  undeclared for 0.1.0; recorded so that a run reads Excel's double for the search in
  `spikes/exsheet-financial-evaluation/`).
- These 31 stay `uncertain`. `-Update` does not touch a recorded case.

## Blocked, and why (13)

| Case | File | Source | Why (`oracleSkip`, or the oracle's own reason) |
|---|---|---|---|
| TABLE-005 to TABLE-010 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| NAME-006, NAME-007, NAME-008 | `sheet-names.json` | documented | Excel reads a qualifier naming no sheet as an external workbook and may open a file dialog; ask by hand |
| NAME-036 | `sheet-names.json` | documented | Excel's undo does not reach changes made through COM; ask by hand |
| TEXTFN-008 | `text.json` | observed | needs ja-JP; this machine's regional format is en-US (asked under ja-JP by the first pass) |
| TEXTFN-009, TEXTFN-010 | `text.json` | observed | needs de-DE (asked under de-DE by the first pass) |

## Agreed and promoted (66)

`summary.md`, "Agreed and promoted", lists each with its Formula and Excel's answer. By file: AGGREGATE-062 to 067; DAY-005;
DAYS-005; EDATE-013 to 015; EOMONTH-012 to 015; FV-011, FV-012, FV-013, FV-019; INDIRECT-045 to 050; LARGE-013, LARGE-014;
MOD-018, MOD-019; MONTH-005; NETWORKDAYS-009; PMT-019, PMT-022, PMT-033; PV-008, PV-009, PV-010; SMALL-007, SMALL-009,
SMALL-010; SORT-016 to 020 and SORT-023 to 025; SORTBY-007 to 009; TEXTFN-029 to 034; UNIQUE-012 to 016; WEEKDAY-008;
WORKDAY-010; XNPV-016; YEAR-008.

- **Every `-0.5` date case agrees with `#NUM!`:** DAY-005, DAYS-005, MONTH-005, NETWORKDAYS-009, WEEKDAY-008,
  WORKDAY-010, YEAR-008 (and EOMONTH-015, `EOMONTH(0,-1)`). EDATE-017, EOMONTH-016 and EOMONTH-017, where the engine
  expects `#VALUE!` for a start of -0.5, are `#NUM!` in Excel (above).
- **The doubles that agree to the last bit:** FV-011 2581.4033740601185, FV-012 2098.0640302830534, FV-013
  -2040.8618907559849, FV-019 483.06382918288546, MOD-018 0.19999216773887385, MOD-019 -0.19999216773887385, PMT-019
  -147.95035898410148, PMT-022 -196.49866329970342, PMT-033 -120.53017247652841, PV-008 196.86891402364611, PV-009
  -1257.7892535548831, PV-010 48.19985410293382, XNPV-016 -4546.946936260144.

## Still not `observed` in the 25 files (104)

- 63 disagreements (above), all `uncertain`.
- 31 recorded (above), all `uncertain`.
- 10 blocked and `documented`: TABLE-005 to TABLE-010, NAME-006, NAME-007, NAME-008, NAME-036.
- TEXTFN-008, 009 and 010 are blocked here but already `observed`.

## After the pass

- `nix develop -c dotnet test tests/ExSheet.Engine.Tests` over the corpus as `-Update` left it: 5184 passed, 0 failed,
  3 skipped.
- Excel is not running after the pass, and the regional format is en-GB.
- Nothing for the Docs Site: this branch changes the corpus's `source` fields and adds this record, nothing a Consumer
  sees.
