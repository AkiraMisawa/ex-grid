# The area pass before `0.1.0-rc.1`, 2026-10-12

One `oracle.ps1 -Area` pass, decided with the user on 2026-10-12 (ADR-0208's remedy; the notes to ADR-0202 and ADR-0208,
PR #87). It asks every question the seventeenth Windows run's corrections added, and PMT at small rates and NPV. It
follows `docs/specs/exsheet/verify-on-windows-17.md`, Part A, steps 1 to 5, over a smaller scope. **Nothing was
decided.** The disagreements are listed for a human, as `oracle.ps1` says. `-Update` rewrote `source` to `observed` only
for the cases that agreed in the same call.

- **Commit asked:** `75ee684d` (`origin/main`), on branch `claude/exsheet-windows-area-2026-10-12`.
- **Machine:** the seventeenth run's (Windows 11 Pro 25H2, build 26200.9457). Excel 16.0.20430.20146 x64, Current Channel
  (`EXCEL.EXE 16.0.20430.20146`), English (UK) user interface.
- **Regional format:** en-GB before the pass. Set to en-US for the COM and keys passes, then de-DE and ja-JP for the
  three TEXT cases that name them, then put back to en-GB at 10:39:48. `HKCU\Control Panel\International` exported before
  and after reads the same (SHA-256 `071c2b47…456b1af`, `logs/restore-culture.log`). The user had said a change of
  regional format need not be asked each time.
- **Files:** `aggregate, and, column-widths, cos, cot, coth, edate, eomonth, fv, index, indirect, large, mod,
  networkdays, npv, or, pmt, pv, sec, sin, small, sort, subtotal, tan, text, unique, workday, xnpv, xor, sheet-names`
  (30). They hold every `uncertain` case on `main` (114, in 29 files) and the 7 `documented` ones in `index`, `npv` and
  `sheet-names`.
- **Time:** 10:36:39 to 10:39:31 for the six passes.

## NPV-001, NPV-002 and every PMT case

| Case | Source before | Formula | Outcome | Excel's Value2 | Excel's text | Expected | How far apart |
|---|---|---|---|---|---|---|---|
| NPV-001 | documented | `=NPV(10%,-10000,3000,4200,6800)` | **disagree** | 1188.4434123352207 | `$1,188.44 ` | 1188.4434123352216 (12 decimals) | 4 units in the last place, 7.7e-16 |
| NPV-002 | documented | `=NPV(A1,A3:A7)+A2` | **disagree** | 1922.0615549323702 | `$1,922.06 ` | 1922.061554932363 (12 decimals) | 32 units in the last place, 3.8e-15 |
| PMT-001 | observed | `=PMT(8%/12,10,10000)` | agree | -1037.0320893591522 | `($1,037.03)` | the same | |
| PMT-002 | observed | `=PMT(8%/12,10,10000,0,1)` | agree | -1030.1643271779658 | `($1,030.16)` | the same | |
| PMT-003 | observed | `=PMT(6%/12,18*12,0,50000)` | agree | -129.08116086799092 | `($129.08)` | the same | |
| PMT-004 | observed | `=PMT(0,10,1000)` | agree | -100 | `($100.00)` | the same | |
| PMT-005 | observed | `=PMT(0.1,0,1000)` | agree | `#NUM!` | `#NUM!` | the same | |
| PMT-006 | observed | `=PMT(C2,10,1000)` | agree | `#DIV/0!` | `#DIV/0!` | the same | |
| PMT-007 | observed | `=PMT(A6,10,1000)` | agree | `#VALUE!` | `#VALUE!` | the same | |
| PMT-008 | observed | `=PMT(0.1,10,1000,0,2)` | agree | -147.95035898410148 | `($147.95)` | the same | |
| PMT-009 | uncertain | `=PMT(0.1,10,1000,0,0.5)` | **disagree** | -147.95035898410148 | `($147.95)` | `#VALUE!` | Excel answers; a type of 0.5 is read as "at the beginning" |
| PMT-010 | uncertain | `=PMT(0.1,10,1000,0,-1)` | agree, promoted | -147.95035898410148 | `($147.95)` | the same | |
| PMT-011 | uncertain | `=PMT(8%/12,10.5,10000)` | **disagree** | -989.2729401451879 | `($989.27)` | `#VALUE!` | Excel answers for 10.5 periods |
| PMT-012 | uncertain | `=PMT(8%/12,-10,10000)` | **disagree** | 970.3654226924854 | `$970.37 ` | `#VALUE!` | Excel answers for −10 periods |
| PMT-013 | uncertain | `=PMT(0.05/12,36,250000,-5000)` | **disagree** | -7363.7031239763755 | `($7,363.70)` | -7363.703123976373 | 3 units in the last place |
| PMT-014 | uncertain | `=PMT(0.0725/12,12,250000,0,1)` | **disagree** | -21530.430018725485 | `($21,530.43)` | -21530.430018725492 | 2 units in the last place |
| PMT-015 | uncertain | `=PMT(0.05/12,12,10000,0,1)` | **disagree** | -852.5226402171 | `($852.52)` | -852.5226402170998 | 2 units in the last place |
| PMT-016 | uncertain | `=PMT(0.045/12,12,250000,-5000)` | **disagree** | -20936.487788661154 | `($20,936.49)` | -20936.48778866115 | 1 unit in the last place |
| PMT-017 | uncertain | `=PMT(1/10000000,12,1000)` | agree, promoted | -83.33338750000993 | `($83.33)` | the same | |
| PMT-018 | uncertain | `=PMT(0.0365/365,365,10000)` | **disagree** | -27.901671561224465 | `($27.90)` | -27.90167156122446 | 1 unit in the last place |

- **At small rates**: PMT-017 (a rate of 1e-7) agrees to the last bit. PMT-018 (0.0365/365 a day, 365 periods) differs
  in its last bit.
- **The monthly rates of PMT-013 to PMT-016**, and NPV-005 to NPV-008, also differ by one to four units in the last
  place. FV-009 and PV-007 do too. NPV-002 differs by 32, at 3.8e-15.
- NPV-001, NPV-002 and NPV-005 to NPV-008 carry `"digits": 12`, which `oracle.ps1` compares as
  `Math.Round(x, 12)`, twelve decimal places. Each differs within the twelfth decimal place, so each disagrees.
- **Where only the last digits differ**, the text Excel shows (in the currency format it gave the cell) is the text the
  engine's number would show. PMT-009, PMT-011 and PMT-012 differ on screen: Excel shows a payment where the engine
  shows `#VALUE!`.

## Counts

Before (`75ee684d`) and after the pass, the 30 files (`counts-before.md`, `counts-after.md`; `count.py` wrote both):

| | cases | observed | documented | uncertain |
|---|---|---|---|---|
| before | 481 | 360 | 7 | 114 |
| after | 481 | 439 | 6 | 36 |

- `-Update` rewrote `source` on 79 cases: 78 `uncertain` → `observed`, and 1 `documented` → `observed` (INDEX-014).
  `git diff` touches no other line.
- **The whole corpus after the pass**: observed 3018, documented 12, uncertain 36. The `documented` ones outside the 30
  files are TABLE-005 to TABLE-010 (`linked-tables.json`), which the pass did not ask.
- **Cultures**: every case of the 30 files names none (en-US), except three in `text`: TEXTFN-008 (ja-JP), TEXTFN-009
  and TEXTFN-010 (de-DE).

## The passes

| Results file (`tests/ExSheet.Engine.Tests/ExcelOracle/`) | Regional format | How | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|
| `results-2026-10-12-area-com-en-US.json` | en-US | COM, the 30 files, `-Update` | 422 | 29 | 21 | 9 |
| `results-2026-10-12-area-keys-en-US.json` | en-US | keys, the 11 "ask by keys", `-Update` | 11 | 0 | 0 | 0 |
| `results-2026-10-12-area-com-de-DE.json` | de-DE | COM, TEXTFN-009 and 010, `-Update` | 2 | 0 | 0 | 0 |
| `results-2026-10-12-area-keys-de-DE.json` | de-DE | keys, the same two, `-Update` | 2 | 0 | 0 | 0 |
| `results-2026-10-12-area-com-ja-JP.json` | ja-JP | COM, TEXTFN-008, `-Update` | 1 | 0 | 0 | 0 |
| `results-2026-10-12-area-keys-ja-JP.json` | ja-JP | keys, TEXTFN-008, `-Update` | 1 | 0 | 0 | 0 |

- **The keys pass** asked every case the COM pass blocked with "ask by keys": CW-018, CW-021 to CW-023, CW-025 and CW-027
  to CW-032. Excel was visible and in front, and the user was told before it started. No case raised a dialog.
- **The 21 blocked in the COM pass**: 11 "ask by keys" (asked by keys, all agree); 3 that need another regional format
  (asked under it, all agree); and the 7 below.
- Each pass's log is in `logs/`, and the script that ran them is in `scripts/`. `summarise.py` reads the results and
  the corpus before and after, and writes `summary.md`.

## Every disagreement (29)

| Case | File | Source | Formula | Excel's Value2 | Excel's text | Expected |
|---|---|---|---|---|---|---|
| AGGREGATE-060 | `aggregate.json` | uncertain | `=AGGREGATE(15,6,A1:A11,1.2)` | 30 | `30` | `#VALUE!` |
| AGGREGATE-061 | `aggregate.json` | uncertain | `=AGGREGATE(14,6,"5",1)` | `#NUM!` | `#NUM!` | `#VALUE!` |
| EDATE-010 | `edate.json` | uncertain | `=EDATE(60,1)` | 89 | `89` | `#VALUE!` |
| EOMONTH-010 | `eomonth.json` | uncertain | `=EOMONTH(0,1)` | 59 | `59` | `#VALUE!` |
| FV-006 | `fv.json` | uncertain | `=FV(0.06/12,10.5,-200)` | 2150.588229777517 | `$2,150.59 ` | `#VALUE!` |
| FV-007 | `fv.json` | uncertain | `=FV(0.06/12,-10,-200)` | -1946.0823721571962 | `($1,946.08)` | `#VALUE!` |
| FV-009 | `fv.json` | uncertain | `=FV(0.05/12,12,-150,0,1)` | 1849.5026084246633 | `$1,849.50 ` | 1849.502608424663 (1 unit in the last place) |
| LARGE-011 | `large.json` | uncertain | `=LARGE(A1:B5,0.5)` | `#NUM!` | `#NUM!` | `#VALUE!` |
| MOD-017 | `mod.json` | uncertain | `=MOD(123456789012,0.7)` | 0.5000078322611261 | `0.500007832` | 0.5000152587890625 (differs from the sixth significant digit, 1.5e-5) |
| NPV-001 | `npv.json` | documented | `=NPV(10%,-10000,3000,4200,6800)` | 1188.4434123352207 | `$1,188.44 ` | 1188.4434123352216 (12 decimals) |
| NPV-002 | `npv.json` | documented | `=NPV(A1,A3:A7)+A2` | 1922.0615549323702 | `$1,922.06 ` | 1922.061554932363 (12 decimals) |
| NPV-005 | `npv.json` | uncertain | `=NPV(0.08,8000,9200,10000,12000,14500)` | 41922.06155493237 | `$41,922.06 ` | 41922.06155493236 (12 decimals) |
| NPV-006 | `npv.json` | uncertain | `=NPV(A1,A3:A7)` | 41922.06155493237 | `$41,922.06 ` | 41922.06155493236 (12 decimals) |
| NPV-007 | `npv.json` | uncertain | `=NPV(0.1,A1:A4)` | 1188.4434123352207 | `$1,188.44 ` | 1188.4434123352216 (12 decimals) |
| NPV-008 | `npv.json` | uncertain | `=NPV(A1,-10000,3000,4200,6800)` | 1188.4434123352207 | `$1,188.44 ` | 1188.4434123352216 (12 decimals) |
| PMT-009 | `pmt.json` | uncertain | `=PMT(0.1,10,1000,0,0.5)` | -147.95035898410148 | `($147.95)` | `#VALUE!` |
| PMT-011 | `pmt.json` | uncertain | `=PMT(8%/12,10.5,10000)` | -989.2729401451879 | `($989.27)` | `#VALUE!` |
| PMT-012 | `pmt.json` | uncertain | `=PMT(8%/12,-10,10000)` | 970.3654226924854 | `$970.37 ` | `#VALUE!` |
| PMT-013 | `pmt.json` | uncertain | `=PMT(0.05/12,36,250000,-5000)` | -7363.7031239763755 | `($7,363.70)` | -7363.703123976373 |
| PMT-014 | `pmt.json` | uncertain | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725485 | `($21,530.43)` | -21530.430018725492 |
| PMT-015 | `pmt.json` | uncertain | `=PMT(0.05/12,12,10000,0,1)` | -852.5226402171 | `($852.52)` | -852.5226402170998 |
| PMT-016 | `pmt.json` | uncertain | `=PMT(0.045/12,12,250000,-5000)` | -20936.487788661154 | `($20,936.49)` | -20936.48778866115 |
| PMT-018 | `pmt.json` | uncertain | `=PMT(0.0365/365,365,10000)` | -27.901671561224465 | `($27.90)` | -27.90167156122446 |
| PV-005 | `pv.json` | uncertain | `=PV(0.05,10.5,-100)` | 801.7640221710038 | `$801.76 ` | `#VALUE!` |
| PV-007 | `pv.json` | uncertain | `=PV(0.05/12,12,-150,0,1)` | 1759.484064397425 | `$1,759.48 ` | 1759.4840643974248 (1 unit in the last place) |
| SMALL-004 | `small.json` | uncertain | `=SMALL(A1:A9,1.5)` | 2 | `2` | `#VALUE!` |
| SMALL-005 | `small.json` | uncertain | `=SMALL(A1:A9,1.2)` | 2 | `2` | `#VALUE!` |
| TEXTFN-025 | `text.json` | uncertain | `=TEXT(-1/3,"General")` | `-0.333333333` (text) | `-0.333333333` | `#VALUE!` |
| TEXTFN-028 | `text.json` | uncertain | `=TEXT(1/3,"@")` | `0.333333333` (text) | `0.333333333` | `#VALUE!` |

They fall into three kinds. This is a description of what Excel answered, not a decision:

- **The engine refuses with `#VALUE!` where Excel answers (13).** Excel truncates a fractional count or type (AGGREGATE-060's k of 1.2,
  FV-006, PMT-009, PMT-011, PV-005, SMALL-004 and SMALL-005). Excel answers a negative count of periods (FV-007, PMT-012).
  Excel takes serial 60 and serial 0 as dates (EDATE-010, EOMONTH-010). TEXT formats with `General` and `@` (TEXTFN-025,
  TEXTFN-028).
- **Another error (2):** Excel answers `#NUM!` where the engine expects `#VALUE!` (AGGREGATE-061, LARGE-011).
- **The number differs (14):** NPV-001, NPV-002, NPV-005 to NPV-008, PMT-013 to PMT-016, PMT-018, FV-009 and PV-007 in
  their last digits (1 to 32 units in the last place). MOD-017 differs from the sixth significant digit, and so does its
  text.

## Blocked, and why (7)

| Case | File | Source | Why (`oracleSkip`) |
|---|---|---|---|
| CW-013 | `column-widths.json` | observed | Excel's undo does not reach changes made through COM |
| CW-014 | `column-widths.json` | observed | Excel's undo does not reach changes made through COM |
| CW-026 | `column-widths.json` | observed | A value set through COM never widens a column, and Excel's undo does not reach changes made through COM: asked by hand (verification/2026-10-11-windows-17, b10) |
| NAME-006 | `sheet-names.json` | documented | Excel reads a qualifier naming no sheet as an external workbook and may open a file dialog; ask by hand |
| NAME-007 | `sheet-names.json` | documented | the same |
| NAME-008 | `sheet-names.json` | documented | the same |
| NAME-036 | `sheet-names.json` | documented | Excel's undo does not reach changes made through COM; ask by hand |

## Recorded: the engine refuses by decision, Excel's answer is read and not compared (9)

| Case | File | Excel's Value2 | Excel's text | The engine | The decision |
|---|---|---|---|---|---|
| INDIRECT-036 | `indirect.json` | 45 | `45` | `#VALUE!` | ADR-0124: R1C1 text in a form no run has observed (relative `R[1]C[-1]` from C2) |
| INDIRECT-037 | `indirect.json` | 45 | `45` | `#VALUE!` | ADR-0124, the same |
| INDIRECT-038 | `indirect.json` | 118.333 | `118.333` | `#VALUE!` | ADR-0124, the same |
| INDIRECT-043 | `indirect.json` | 250.5 | `250.5` | `#VALUE!` | ADR-0124: structured reference text, a table's bare name included |
| SORT-012 | `sort.json` | 0 | `0` | an empty cell | ADR-0202, ADR-0047: a blank among SORT's keys is refused until Excel is asked where it sorts |
| SORT-013 | `sort.json` | 7 | `7` | `#VALUE!` | the same |
| SORT-014 | `sort.json` | `a` (text) | `a` | `#VALUE!` | the same |
| UNIQUE-010 | `unique.json` | 0 | `0` | an empty cell | ADR-0202, ADR-0047: a blank in UNIQUE's array is refused until Excel is asked whether it is one of a kind with 0 |
| UNIQUE-011 | `unique.json` | 2 | `2` | `#VALUE!` | the same |

These nine stay `uncertain`. `-Update` does not touch a recorded case.

## Every case, by file

`summary.md`, "Every case the pass asked, by file", lists every case of the 30 files once, by the pass of record:
agreed (and of those, promoted), disagreed, blocked, recorded.

## Still not `observed` in the 30 files (42)

- 29 disagreements (above). 27 are `uncertain`. NPV-001 and NPV-002 are `documented`.
- 9 recorded (above), all `uncertain`.
- 4 blocked and `documented`: NAME-006, NAME-007, NAME-008, NAME-036, each with an `oracleSkip` reason.
- CW-013, CW-014 and CW-026 are blocked but were already `observed`.

## After the pass

- `nix develop -c dotnet test tests/ExSheet.Engine.Tests` over the corpus as `-Update` left it: 4762 passed, 0 failed.
- Nothing for the Docs Site: this branch changes the corpus's `source` fields and adds this record, nothing a Consumer
  sees.
