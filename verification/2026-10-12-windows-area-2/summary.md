## Every disagreement

| Case | File | Source before | Formula | Excel | Excel's text | Expected | Units in the last place | What differs |
|---|---|---|---|---|---|---|---|---|
| EDATE-016 | `edate.json` | uncertain | `=EDATE(0,1)` | 31 | `31` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 31 (number) |
| EDATE-017 | `edate.json` | uncertain | `=EDATE(-0.5,1)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| EOMONTH-016 | `eomonth.json` | uncertain | `=EOMONTH(-0.5,0)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| EOMONTH-017 | `eomonth.json` | uncertain | `=EOMONTH(-0.5,1)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| FV-014 | `fv.json` | uncertain | `=FV(0.05/12,12,-150,-1000,1)` | 2900.664506306397 | `$2,900.66 ` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2900.664506306397 (number) |
| FV-015 | `fv.json` | uncertain | `=FV(0.03/12,240,-100,-50000,1)` | 123950.02507801351 | `$123,950.03 ` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 123950.02507801351 (number) |
| FV-016 | `fv.json` | uncertain | `=FV(0.42,0.5,-100)` | 45.627983043166275 | `$45.63 ` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 45.627983043166275 (number) |
| FV-017 | `fv.json` | uncertain | `=FV(-9,1/3,-1)` | 0.33333333333333331 | `$0.33 ` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0.33333333333333331 (number) |
| FV-018 | `fv.json` | uncertain | `=FV(-1,0,-100,-500)` | `#NUM!` | `#NUM!` | `500` |  | value2: expected 500, Excel "#NUM!" (error) |
| FV-020 | `fv.json` | uncertain | `=FV(-1,-0.5,-100)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#DIV/0!" (error) |
| FV-021 | `fv.json` | uncertain | `=FV(-1,-10,-100)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#DIV/0!" (error) |
| FV-022 | `fv.json` | uncertain | `=FV(0.06/12,-2.5,-100)` | -247.8287941856272 | `($247.83)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -247.8287941856272 (number) |
| MOD-020 | `mod.json` | uncertain | `=MOD(1,0.1)` | 0.09999999999999995 | `0.1` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0.09999999999999995 (number) |
| MOD-021 | `mod.json` | uncertain | `=MOD(5,0.1)` | 0.099999999999999728 | `0.1` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0.099999999999999728 (number) |
| MOD-022 | `mod.json` | uncertain | `=MOD(-2.2,0.1)` | 0.09999999999999995 | `0.1` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0.09999999999999995 (number) |
| MOD-023 | `mod.json` | uncertain | `=MOD(-1,-0.1)` | -0.09999999999999995 | `-0.1` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -0.09999999999999995 (number) |
| MOD-024 | `mod.json` | uncertain | `=MOD(3.3,0.01)` | 0.0099999999999997539 | `0.01` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0.0099999999999997539 (number) |
| MOD-025 | `mod.json` | uncertain | `=MOD(0.7,0.01)` | 0.0099999999999999412 | `0.01` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0.0099999999999999412 (number) |
| MOD-026 | `mod.json` | uncertain | `=MOD(100000000000,0.1)` | 0.09999444888487688 | `0.099994449` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0.09999444888487688 (number) |
| MOD-027 | `mod.json` | uncertain | `=MOD(123456789012,0.2)` | 0.19999314677151464 | `0.199993147` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0.19999314677151464 (number) |
| MOD-028 | `mod.json` | uncertain | `=MOD(-693380,-93.7)` | -93.699999999978971 | `-93.7` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -93.699999999978971 (number) |
| MOD-029 | `mod.json` | uncertain | `=MOD(-0.087626,6630)` | 6629.9123739999995 | `6629.912374` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 6629.9123739999995 (number) |
| MOD-030 | `mod.json` | uncertain | `=MOD(1.1,0.1)` | 2.7755575615628914E-17 | `2.77556E-17` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2.7755575615628914E-17 (number) |
| MOD-031 | `mod.json` | uncertain | `=MOD(-0.9,0.1)` | 2.7755575615628914E-17 | `2.77556E-17` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2.7755575615628914E-17 (number) |
| MOD-032 | `mod.json` | uncertain | `=MOD(-1E-20,1)` | 0 | `0` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0 (number) |
| PMT-013 | `pmt.json` | uncertain | `=PMT(0.05/12,36,250000,-5000)` | -7363.7031239763755 | `($7,363.70)` | -7363.703123976373 | 3 (2.7e-12) | value2: expected -7363.703123976373, Excel -7363.7031239763755 (number) |
| PMT-014 | `pmt.json` | uncertain | `=PMT(0.0725/12,12,250000,0,1)` | -21530.430018725485 | `($21,530.43)` | -21530.430018725492 | 2 (7.3e-12) | value2: expected -21530.430018725492, Excel -21530.430018725485 (number) |
| PMT-015 | `pmt.json` | uncertain | `=PMT(0.05/12,12,10000,0,1)` | -852.52264021710005 | `($852.52)` | -852.5226402170998 | 2 (2.3e-13) | value2: expected -852.5226402170998, Excel -852.5226402171 (number) |
| PMT-016 | `pmt.json` | uncertain | `=PMT(0.045/12,12,250000,-5000)` | -20936.487788661154 | `($20,936.49)` | -20936.48778866115 | 1 (3.6e-12) | value2: expected -20936.48778866115, Excel -20936.487788661154 (number) |
| PMT-018 | `pmt.json` | uncertain | `=PMT(0.0365/365,365,10000)` | -27.901671561224465 | `($27.90)` | -27.90167156122446 | 1 (3.6e-15) | value2: expected -27.90167156122446, Excel -27.901671561224465 (number) |
| PMT-020 | `pmt.json` | uncertain | `=PMT(0.15,12,1000,-5000,1)` | -10.501648240558216 | `($10.50)` | -10.501648240558218 | 1 (1.8e-15) | value2: expected -10.501648240558218, Excel -10.501648240558216 (number) |
| PMT-021 | `pmt.json` | uncertain | `=PMT(0.15,60,250000,-5000,1)` | -32615.986669943602 | `($32,615.99)` | -32615.986669943606 | 1 (3.6e-12) | value2: expected -32615.986669943606, Excel -32615.986669943602 (number) |
| PMT-023 | `pmt.json` | uncertain | `=PMT(0.0365/365,24,250000,20000,1)` | -11260.941796175353 | `($11,260.94)` | -11260.941796175352 | 1 (1.8e-12) | value2: expected -11260.941796175352, Excel -11260.941796175353 (number) |
| PMT-024 | `pmt.json` | uncertain | `=PMT(0.0525/12,24,250000,-5000,1)` | -10750.7823801456 | `($10,750.78)` | -10750.782380145603 | 2 (3.6e-12) | value2: expected -10750.782380145603, Excel -10750.7823801456 (number) |
| PMT-025 | `pmt.json` | uncertain | `=PMT(0.065/12,60,1000,-5000,1)` | 50.905521301199329 | `$50.91 ` | 50.90552130119935 | 3 (2.1e-14) | value2: expected 50.90552130119935, Excel 50.905521301199329 (number) |
| PMT-026 | `pmt.json` | uncertain | `=PMT(0.07/12,240,1000,-5000)` | 1.8452907580883169 | `$1.85 ` | 1.8452907580883144 | 11 (2.4e-15) | value2: expected 1.8452907580883144, Excel 1.8452907580883169 (number) |
| PMT-027 | `pmt.json` | uncertain | `=PMT(0.00001,360,10000,-5000,1)` | -13.963833694802407 | `($13.96)` | -13.963833694802405 | 1 (1.8e-15) | value2: expected -13.963833694802405, Excel -13.963833694802407 (number) |
| PMT-028 | `pmt.json` | uncertain | `=PMT(0.025/4,240,1000,-5000)` | 0.97372774207228763 | `$0.97 ` | 0.9737277420722884 | 7 (7.8e-16) | value2: expected 0.9737277420722884, Excel 0.97372774207228763 (number) |
| PMT-029 | `pmt.json` | uncertain | `=PMT(0.06/52,60,1000,-5000,1)` | 63.197272167756296 | `$63.20 ` | 63.197272167756275 | 3 (2.1e-14) | value2: expected 63.197272167756275, Excel 63.197272167756296 (number) |
| PMT-030 | `pmt.json` | uncertain | `=PMT(0.045/12,12,10000,-5000)` | -445.64260793186031 | `($445.64)` | -445.64260793186025 | 1 (5.7e-14) | value2: expected -445.64260793186025, Excel -445.64260793186031 (number) |
| PMT-031 | `pmt.json` | uncertain | `=PMT(0.001,12,250000,20000,1)` | -22603.914076303507 | `($22,603.91)` | -22603.91407630351 | 1 (3.6e-12) | value2: expected -22603.91407630351, Excel -22603.914076303507 (number) |
| PMT-032 | `pmt.json` | uncertain | `=PMT(0.03/12,12,250000,20000)` | -22817.298664792012 | `($22,817.30)` | -22817.298664792015 | 1 (3.6e-12) | value2: expected -22817.298664792015, Excel -22817.298664792012 (number) |
| PV-011 | `pv.json` | uncertain | `=PV(-1.5,0.5,-100)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| PV-012 | `pv.json` | uncertain | `=PV(-1,-10,-100)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#DIV/0!" (error) |
| SMALL-008 | `small.json` | uncertain | `=SMALL(A1:A9,9.5)` | `#NUM!` | `#NUM!` | `7` |  | value2: expected 7, Excel "#NUM!" (error) |
| SORT-015 | `sort.json` | uncertain | `=SORT(A1:A2)` | 7 | `7` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 7 (number) |
| SORT-021 | `sort.json` | uncertain | `=COUNTA(SORT(Q1:Q2))` | 2 | `2` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2 (number) |
| SORT-022 | `sort.json` | uncertain | `=ISBLANK(INDEX(SORT(Q1:Q2),2))` | `True` | `TRUE` | `#VALUE!` |  | value2: expected "#VALUE!", Excel true (boolean) |
| UNIQUE-017 | `unique.json` | uncertain | `=COUNTA(UNIQUE(Q1:Q2))` | 2 | `2` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2 (number) |
| UNIQUE-018 | `unique.json` | uncertain | `=INDEX(UNIQUE(Q1:Q2),2)=""` | `True` | `TRUE` | `#VALUE!` |  | value2: expected "#VALUE!", Excel true (boolean) |
| UNIQUE-019 | `unique.json` | uncertain | `=SUM(--(LEN(UNIQUE(A1:A3))>0))` | 2 | `2` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2 (number) |
| XNPV-005 | `xnpv.json` | uncertain | `=XNPV(0.07,A1:A4,B1:B4)` | 2023.0065393622904 | `2023.006539` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2023.0065393622904 (number) |
| XNPV-006 | `xnpv.json` | uncertain | `=XNPV(0.06,A1:A4,B1:B4)` | 2277.5848519247406 | `2277.584852` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2277.5848519247406 (number) |
| XNPV-007 | `xnpv.json` | uncertain | `=XNPV(-1,A1:A3,B1:B3)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-008 | `xnpv.json` | uncertain | `=XNPV(-1,A1:A2,B1:B2)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-009 | `xnpv.json` | uncertain | `=XNPV(-1.5,A1:A3,B1:B3)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-010 | `xnpv.json` | uncertain | `=XNPV(-2,A1:A2,B1:B2)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-011 | `xnpv.json` | uncertain | `=XNPV(0.05,A1:A2,B1:B2)` | -7286.8778090094384 | `-7286.877809` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -7286.8778090094384 (number) |
| XNPV-012 | `xnpv.json` | uncertain | `=XNPV(-0.5,A1:A7,B1:B7)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-013 | `xnpv.json` | uncertain | `=XNPV(0.1,A1:A6,B1:B6)` | 700.22697700285767 | `700.226977` | 700.2269770028581 | 4 (4.5e-13) | value2: expected 700.2269770028581, Excel 700.22697700285767 (number) |
| XNPV-014 | `xnpv.json` | uncertain | `=XNPV(0.06,A1:A5,B1:B5)` | 5895.8664861795514 | `5895.866486` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 5895.8664861795514 (number) |
| XNPV-015 | `xnpv.json` | uncertain | `=XNPV(0.07,A1:A7,B1:B7)` | 4384.9427145314748 | `4384.942715` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 4384.9427145314748 (number) |
| XNPV-017 | `xnpv.json` | uncertain | `=XNPV(-0.5,A1:A7,B1:B7)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |

## Blocked, and why

| Case | File | Source | Why |
|---|---|---|---|
| TABLE-005 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-006 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-007 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-008 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-009 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-010 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| NAME-006 | `sheet-names.json` | documented | Excel reads a qualifier naming no sheet as an external workbook and may open a file dialog; ask by hand |
| NAME-007 | `sheet-names.json` | documented | Excel reads a qualifier naming no sheet as an external workbook and may open a file dialog; ask by hand |
| NAME-008 | `sheet-names.json` | documented | Excel reads a qualifier naming no sheet as an external workbook and may open a file dialog; ask by hand |
| NAME-036 | `sheet-names.json` | documented | Excel's undo does not reach changes made through COM; ask by hand |
| TEXTFN-008 | `text.json` | observed | needs ja-JP; this machine's regional format is en-US |
| TEXTFN-009 | `text.json` | observed | needs de-DE; this machine's regional format is en-US |
| TEXTFN-010 | `text.json` | observed | needs de-DE; this machine's regional format is en-US |

## Recorded (the engine refuses by decision; Excel's answer is read, not compared)

| Case | File | Formula | Excel | Excel's text | The engine | Decision |
|---|---|---|---|---|---|---|
| INDIRECT-044 | `indirect.json` | `=INDIRECT("RC[-1]",FALSE)` | 1.333 | `1.333` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-051 | `indirect.json` | `=SUM(INDIRECT("RC[-1]:R[1]C[-1]",FALSE))` | 46.332999999999998 | `46.333` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-052 | `indirect.json` | `=INDIRECT("R[-5]C[-1]",FALSE)` | 0 | `0` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): an offset past the Sheet's edge is #VALUE! until a run observes it,… |
| INDIRECT-053 | `indirect.json` | `=COLUMNS(INDIRECT("Positions"))` | 2 | `2` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-10, 2026-10-12): a declared Linked Table's bare name is #VALUE! until a … |
| INDIRECT-054 | `indirect.json` | `=ROWS(INDIRECT("Positions"))` | 2 | `2` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-10, 2026-10-12): a declared Linked Table's bare name is #VALUE! until a … |
| INDIRECT-055 | `indirect.json` | `=INDEX(INDIRECT("Positions"),1,1)` | `R-1` (text) | `R-1` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-10, 2026-10-12): a declared Linked Table's bare name is #VALUE! until a … |
| INDIRECT-056 | `indirect.json` | `=COUNTA(INDIRECT("Positions"))` | 4 | `4` | `{"value2": 1}` | ADR-0124 (2026-10-10, 2026-10-12): a declared Linked Table's bare name is #VALUE! until a … |
| INDIRECT-057 | `indirect.json` | AAA1000, spilled by `=INDIRECT("Positions")` in ZZ1000 | 250.5 | `250.5` | `{"value2": null}` | ADR-0124 (2026-10-10, 2026-10-12): a declared Linked Table's bare name is #VALUE! until a … |
| INDIRECT-058 | `indirect.json` | `=SUM(INDIRECT("R2C2:R3",FALSE))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-059 | `indirect.json` | `=INDIRECT("R03C2",FALSE)` | 45 | `45` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-060 | `indirect.json` | `=SUM(INDIRECT("R1C1#",FALSE))` | 6 | `6` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-061 | `indirect.json` | `=INDIRECT("Positions[PV]",FALSE)` | 250.5 | `250.5` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-062 | `indirect.json` | `=ROWS(INDIRECT("R3:C2",FALSE))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-063 | `indirect.json` | `=INDIRECT("R[+1]C[-1]",FALSE)` | 45 | `45` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-064 | `indirect.json` | `=INDIRECT("R[0]C[-1]",FALSE)` | 1.333 | `1.333` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-065 | `indirect.json` | `=INDIRECT("R[-0]C[-1]",FALSE)` | 1.333 | `1.333` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| INDIRECT-066 | `indirect.json` | `=INDIRECT("R[2]C",FALSE)` | 45 | `45` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): R1C1 text in a form no run has observed, an R or a C with no number… |
| NPV-009 | `npv.json` | `=NPV(0.12,-25000,6000,8000,9000,7500,4000)` | 157.86528462709433 | `$157.87 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-010 | `npv.json` | `=NPV(0.045,-10000,2000,2000,2000,2000,2000,2000,2000)` | 1708.5185462007141 | `$1,708.52 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-011 | `npv.json` | `=NPV(0.045,100,200,300,400,500,600,700,800,900,1000)` | 4065.5859439548863 | `$4,065.59 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-012 | `npv.json` | `=NPV(0.05,-25000,6000,8000,9000,7500,4000)` | 4808.9839576631866 | `$4,808.98 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-013 | `npv.json` | `=NPV(0.08,-120000,39000,30000,21000,37000,46000)` | 15745.078146860713 | `$15,745.08 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-014 | `npv.json` | `=NPV(0.09,-10000,2000,2000,2000,2000,2000,2000,2000)` | 60.463917567430045 | `$60.46 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-015 | `npv.json` | `=NPV(0.1,-10000,2000,2000,2000,2000,2000,2000,2000)` | -239.2385132855801 | `($239.24)` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-016 | `npv.json` | `=NPV(0.1,-25000,6000,8000,9000,7500,4000)` | 1303.8501073347138 | `$1,303.85 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-017 | `npv.json` | `=NPV(0.07,-10000,2000,2000,2000,2000,2000,2000,2000)` | 727.64374139943288 | `$727.64 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-018 | `npv.json` | `=NPV(0.07,-25000,6000,8000,9000,7500,4000)` | 3285.3515532789606 | `$3,285.35 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-019 | `npv.json` | `=NPV(0.045,-120000,39000,30000,21000,37000,46000)` | 29793.511231516124 | `$29,793.51 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-020 | `npv.json` | `=NPV(0.08,-7500,3000,3000,3000)` | 214.15829791077385 | `$214.16 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-021 | `npv.json` | `=NPV(0.09,-40000,8000,9200,10000,12000,14500)` | 669.58524225299175 | `$669.59 ` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |
| NPV-022 | `npv.json` | `=NPV(0.15,-25000,6000,8000,9000,7500,4000)` | -1338.223462825282 | `($1,338.22)` | `{"value2": "#NAME?"}` | ADR-0202 (note of 2026-10-12): NPV is undeclared, #NAME?, for 0.1.0; recorded, so that a r… |

## Agreed and promoted

| Case | File | Formula | Excel | Excel's text |
|---|---|---|---|---|
| AGGREGATE-062 | `aggregate.json` | `=AGGREGATE(15,6,A1:A11,0.5)` | `#NUM!` | `#NUM!` |
| AGGREGATE-063 | `aggregate.json` | `=AGGREGATE(14,6,A1:A11,0.5)` | `#NUM!` | `#NUM!` |
| AGGREGATE-064 | `aggregate.json` | `=AGGREGATE(14,6,TRUE,1)` | `#NUM!` | `#NUM!` |
| AGGREGATE-065 | `aggregate.json` | `=AGGREGATE(15,6,"abc",1)` | `#NUM!` | `#NUM!` |
| AGGREGATE-066 | `aggregate.json` | `=AGGREGATE(15,6,A1:A11,8.9)` | 81 | `81` |
| AGGREGATE-067 | `aggregate.json` | `=AGGREGATE(14,6,1/0,1)` | `#DIV/0!` | `#DIV/0!` |
| DAY-005 | `day.json` | `=DAY(-0.5)` | `#NUM!` | `#NUM!` |
| DAYS-005 | `days.json` | `=DAYS(1,-0.5)` | `#NUM!` | `#NUM!` |
| EDATE-013 | `edate.json` | `=EDATE(60,0)` | 59 | `59` |
| EDATE-014 | `edate.json` | `=EDATE(60,12)` | 425 | `425` |
| EDATE-015 | `edate.json` | `=EDATE(60,-1)` | 29 | `29` |
| EOMONTH-012 | `eomonth.json` | `=EOMONTH(60,0)` | 59 | `59` |
| EOMONTH-013 | `eomonth.json` | `=EOMONTH(60,1)` | 91 | `91` |
| EOMONTH-014 | `eomonth.json` | `=EOMONTH(0,0)` | 31 | `31` |
| EOMONTH-015 | `eomonth.json` | `=EOMONTH(0,-1)` | `#NUM!` | `#NUM!` |
| FV-011 | `fv.json` | `=FV(0.06/12,10,-200,-500,0.5)` | 2581.4033740601185 | `$2,581.40 ` |
| FV-012 | `fv.json` | `=FV(0.06/12,10.25,-200)` | 2098.0640302830534 | `$2,098.06 ` |
| FV-013 | `fv.json` | `=FV(0.06/12,-10.5,-200)` | -2040.8618907559849 | `($2,040.86)` |
| FV-019 | `fv.json` | `=FV(0.06/12,10,-150,1000)` | 483.06382918288546 | `$483.06 ` |
| INDIRECT-045 | `indirect.json` | `=INDIRECT("R[-1]C[-1]",FALSE)` | 1.333 | `1.333` |
| INDIRECT-046 | `indirect.json` | `=SUM(INDIRECT("R[1]",FALSE))` | 45 | `45` |
| INDIRECT-047 | `indirect.json` | `=SUM(INDIRECT("C[-1]",FALSE))` | 118.333 | `118.333` |
| INDIRECT-048 | `indirect.json` | `=SUM(INDIRECT("R2:R3",FALSE))` | 46.332999999999998 | `46.333` |
| INDIRECT-049 | `indirect.json` | `=SUM(INDIRECT("C1:C2",FALSE))` | 123.333 | `123.333` |
| INDIRECT-050 | `indirect.json` | `=INDIRECT("R3C[-1]",FALSE)` | 45 | `45` |
| LARGE-013 | `large.json` | `=LARGE(A1:B5,0.99)` | `#NUM!` | `#NUM!` |
| LARGE-014 | `large.json` | `=LARGE("5",1)` | 5 | `5` |
| MOD-018 | `mod.json` | `=MOD(-123456789012,0.7)` | 0.19999216773887385 | `0.199992168` |
| MOD-019 | `mod.json` | `=MOD(123456789012,-0.7)` | -0.19999216773887385 | `-0.199992168` |
| MONTH-005 | `month.json` | `=MONTH(-0.5)` | `#NUM!` | `#NUM!` |
| NETWORKDAYS-009 | `networkdays.json` | `=NETWORKDAYS(-0.5,6)` | `#NUM!` | `#NUM!` |
| PMT-019 | `pmt.json` | `=PMT(0.1,10,1000,0,0.1)` | -147.95035898410148 | `($147.95)` |
| PMT-022 | `pmt.json` | `=PMT(0.09/12,36,10000,-5000)` | -196.49866329970342 | `($196.50)` |
| PMT-033 | `pmt.json` | `=PMT(0.12/12,120,10000,-5000,1)` | -120.53017247652841 | `($120.53)` |
| PV-008 | `pv.json` | `=PV(0.05,10,-100,1000,0.5)` | 196.86891402364611 | `$196.87 ` |
| PV-009 | `pv.json` | `=PV(0.05,-10,-100)` | -1257.7892535548831 | `($1,257.79)` |
| PV-010 | `pv.json` | `=PV(0.05,0.5,-100)` | 48.19985410293382 | `$48.20 ` |
| SMALL-007 | `small.json` | `=SMALL(A1:A9,0.5)` | `#NUM!` | `#NUM!` |
| SMALL-009 | `small.json` | `=SMALL(A1:A9,3.9)` | 3 | `3` |
| SMALL-010 | `small.json` | `=SMALL(TRUE,1)` | 1 | `1` |
| SORT-016 | `sort.json` | `=SORT(A1:A3,1,-1)` | `b` (text) | `b` |
| SORT-017 | `sort.json` | ZZ1002, spilled by `=SORT(A1:A3,1,-1)` in ZZ1000 | 0 | `0` |
| SORT-018 | `sort.json` | ZZ1001, spilled by `=SORT(A1:A3)` in ZZ1000 | `True` | `TRUE` |
| SORT-019 | `sort.json` | ZZ1002, spilled by `=SORT(A1:A3)` in ZZ1000 | 0 | `0` |
| SORT-020 | `sort.json` | AAB1000, spilled by `=SORT(A1:C1,1,1,TRUE)` in ZZ1000 | 0 | `0` |
| SORT-023 | `sort.json` | AAA1001, spilled by `=SORT(A1:B3,1,-1)` in ZZ1000 | `x` (text) | `x` |
| SORT-024 | `sort.json` | ZZ1001, spilled by `=SORT(A1:A3)` in ZZ1000 | `b` (text) | `b` |
| SORT-025 | `sort.json` | ZZ1002, spilled by `=SORT(A1:A3)` in ZZ1000 | 0 | `0` |
| SORTBY-007 | `sortby.json` | `=SORTBY(H1:H2,Z1:Z2)` | 20 | `20` |
| SORTBY-008 | `sortby.json` | `=SORTBY(H1:H2,Z1:Z2,1,AA1:AA2,1)` | 20 | `20` |
| SORTBY-009 | `sortby.json` | ZZ1001, spilled by `=SORTBY(H1:H3,Z1:Z3,1,AA1:AA3,-1)` in ZZ1000 | 20 | `20` |
| TEXTFN-029 | `text.json` | `=TEXT(-123456789012,"General")` | `-1.23457E+11` (text) | `-1.23457E+11` |
| TEXTFN-030 | `text.json` | `=TEXT(-12345678901,"General")` | `-12345678901` (text) | `-12345678901` |
| TEXTFN-031 | `text.json` | `=TEXT(-1/3,"@")` | `-0.333333333` (text) | `-0.333333333` |
| TEXTFN-032 | `text.json` | `=TEXT(123456789012,"@")` | `1.23457E+11` (text) | `1.23457E+11` |
| TEXTFN-033 | `text.json` | `=TEXT(0.00001,"@")` | `0.00001` (text) | `0.00001` |
| TEXTFN-034 | `text.json` | `=TEXT(1/3,"""x""@")` | `0.333333333` (text) | `0.333333333` |
| UNIQUE-012 | `unique.json` | `=ROWS(UNIQUE(A1:A3))` | 2 | `2` |
| UNIQUE-013 | `unique.json` | `=ROWS(UNIQUE(A1:A2))` | 2 | `2` |
| UNIQUE-014 | `unique.json` | `=UNIQUE(A1:A3,,TRUE)` | 1 | `1` |
| UNIQUE-015 | `unique.json` | `=ROWS(UNIQUE(A1:B2))` | 2 | `2` |
| UNIQUE-016 | `unique.json` | `=UNIQUE(Q2)` | 0 | `0` |
| WEEKDAY-008 | `weekday.json` | `=WEEKDAY(-0.5)` | `#NUM!` | `#NUM!` |
| WORKDAY-010 | `workday.json` | `=WORKDAY(-0.5,1)` | `#NUM!` | `#NUM!` |
| XNPV-016 | `xnpv.json` | `=XNPV(0.09,A1:A2,B1:B2)` | -4546.946936260144 | `-4546.946936` |
| YEAR-008 | `year.json` | `=YEAR(-0.5)` | `#NUM!` | `#NUM!` |

## Every case the pass asked, by file

Each case once, by its pass of record. A case promoted is one whose `source` this pass rewrote to `observed`.

| File | agreed | of which promoted | disagreed | blocked | recorded |
|---|---|---|---|---|---|
| `aggregate.json` | 67 | AGGREGATE-062, AGGREGATE-063, AGGREGATE-064, AGGREGATE-065, AGGREGATE-066, AGGREGATE-067 | – | – | – |
| `day.json` | 5 | DAY-005 | – | – | – |
| `days.json` | 5 | DAYS-005 | – | – | – |
| `edate.json` | 15 | EDATE-013, EDATE-014, EDATE-015 | EDATE-016, EDATE-017 | – | – |
| `eomonth.json` | 15 | EOMONTH-012, EOMONTH-013, EOMONTH-014, EOMONTH-015 | EOMONTH-016, EOMONTH-017 | – | – |
| `fv.json` | 14 | FV-011, FV-012, FV-013, FV-019 | FV-014, FV-015, FV-016, FV-017, FV-018, FV-020, FV-021, FV-022 | – | – |
| `indirect.json` | 49 | INDIRECT-045, INDIRECT-046, INDIRECT-047, INDIRECT-048, INDIRECT-049, INDIRECT-050 | – | – | INDIRECT-044, INDIRECT-051, INDIRECT-052, INDIRECT-053, INDIRECT-054, INDIRECT-055, INDIRECT-056, INDIRECT-057, INDIRECT-058, INDIRECT-059, INDIRECT-060, INDIRECT-061, INDIRECT-062, INDIRECT-063, INDIRECT-064, INDIRECT-065, INDIRECT-066 |
| `large.json` | 14 | LARGE-013, LARGE-014 | – | – | – |
| `linked-tables.json` | 17 | – | – | TABLE-005, TABLE-006, TABLE-007, TABLE-008, TABLE-009, TABLE-010 | – |
| `mod.json` | 19 | MOD-018, MOD-019 | MOD-020, MOD-021, MOD-022, MOD-023, MOD-024, MOD-025, MOD-026, MOD-027, MOD-028, MOD-029, MOD-030, MOD-031, MOD-032 | – | – |
| `month.json` | 5 | MONTH-005 | – | – | – |
| `networkdays.json` | 9 | NETWORKDAYS-009 | – | – | – |
| `npv.json` | 8 | – | – | – | NPV-009, NPV-010, NPV-011, NPV-012, NPV-013, NPV-014, NPV-015, NPV-016, NPV-017, NPV-018, NPV-019, NPV-020, NPV-021, NPV-022 |
| `pmt.json` | 16 | PMT-019, PMT-022, PMT-033 | PMT-013, PMT-014, PMT-015, PMT-016, PMT-018, PMT-020, PMT-021, PMT-023, PMT-024, PMT-025, PMT-026, PMT-027, PMT-028, PMT-029, PMT-030, PMT-031, PMT-032 | – | – |
| `pv.json` | 10 | PV-008, PV-009, PV-010 | PV-011, PV-012 | – | – |
| `sheet-names.json` | 33 | – | – | NAME-006, NAME-007, NAME-008, NAME-036 | – |
| `small.json` | 9 | SMALL-007, SMALL-009, SMALL-010 | SMALL-008 | – | – |
| `sort.json` | 22 | SORT-016, SORT-017, SORT-018, SORT-019, SORT-020, SORT-023, SORT-024, SORT-025 | SORT-015, SORT-021, SORT-022 | – | – |
| `sortby.json` | 9 | SORTBY-007, SORTBY-008, SORTBY-009 | – | – | – |
| `text.json` | 31 | TEXTFN-029, TEXTFN-030, TEXTFN-031, TEXTFN-032, TEXTFN-033, TEXTFN-034 | – | TEXTFN-008, TEXTFN-009, TEXTFN-010 | – |
| `unique.json` | 16 | UNIQUE-012, UNIQUE-013, UNIQUE-014, UNIQUE-015, UNIQUE-016 | UNIQUE-017, UNIQUE-018, UNIQUE-019 | – | – |
| `weekday.json` | 8 | WEEKDAY-008 | – | – | – |
| `workday.json` | 10 | WORKDAY-010 | – | – | – |
| `xnpv.json` | 5 | XNPV-016 | XNPV-005, XNPV-006, XNPV-007, XNPV-008, XNPV-009, XNPV-010, XNPV-011, XNPV-012, XNPV-013, XNPV-014, XNPV-015, XNPV-017 | – | – |
| `year.json` | 8 | YEAR-008 | – | – | – |

## The passes

| Results file | Regional format | How | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|
| `results-2026-10-12-area-2-com-en-US.json` | en-US | COM | 419 | 63 | 13 | 31 |

Sources rewritten by `-Update`: uncertain → observed: 66.

Still not `observed` after the pass (104): EDATE-016 (uncertain, disagree), EDATE-017 (uncertain, disagree), EOMONTH-016 (uncertain, disagree), EOMONTH-017 (uncertain, disagree), FV-014 (uncertain, disagree), FV-015 (uncertain, disagree), FV-016 (uncertain, disagree), FV-017 (uncertain, disagree), FV-018 (uncertain, disagree), FV-020 (uncertain, disagree), FV-021 (uncertain, disagree), FV-022 (uncertain, disagree), INDIRECT-044 (uncertain, recorded), INDIRECT-051 (uncertain, recorded), INDIRECT-052 (uncertain, recorded), INDIRECT-053 (uncertain, recorded), INDIRECT-054 (uncertain, recorded), INDIRECT-055 (uncertain, recorded), INDIRECT-056 (uncertain, recorded), INDIRECT-057 (uncertain, recorded), INDIRECT-058 (uncertain, recorded), INDIRECT-059 (uncertain, recorded), INDIRECT-060 (uncertain, recorded), INDIRECT-061 (uncertain, recorded), INDIRECT-062 (uncertain, recorded), INDIRECT-063 (uncertain, recorded), INDIRECT-064 (uncertain, recorded), INDIRECT-065 (uncertain, recorded), INDIRECT-066 (uncertain, recorded), TABLE-005 (documented, blocked), TABLE-006 (documented, blocked), TABLE-007 (documented, blocked), TABLE-008 (documented, blocked), TABLE-009 (documented, blocked), TABLE-010 (documented, blocked), MOD-020 (uncertain, disagree), MOD-021 (uncertain, disagree), MOD-022 (uncertain, disagree), MOD-023 (uncertain, disagree), MOD-024 (uncertain, disagree), MOD-025 (uncertain, disagree), MOD-026 (uncertain, disagree), MOD-027 (uncertain, disagree), MOD-028 (uncertain, disagree), MOD-029 (uncertain, disagree), MOD-030 (uncertain, disagree), MOD-031 (uncertain, disagree), MOD-032 (uncertain, disagree), NPV-009 (uncertain, recorded), NPV-010 (uncertain, recorded), NPV-011 (uncertain, recorded), NPV-012 (uncertain, recorded), NPV-013 (uncertain, recorded), NPV-014 (uncertain, recorded), NPV-015 (uncertain, recorded), NPV-016 (uncertain, recorded), NPV-017 (uncertain, recorded), NPV-018 (uncertain, recorded), NPV-019 (uncertain, recorded), NPV-020 (uncertain, recorded), NPV-021 (uncertain, recorded), NPV-022 (uncertain, recorded), PMT-013 (uncertain, disagree), PMT-014 (uncertain, disagree), PMT-015 (uncertain, disagree), PMT-016 (uncertain, disagree), PMT-018 (uncertain, disagree), PMT-020 (uncertain, disagree), PMT-021 (uncertain, disagree), PMT-023 (uncertain, disagree), PMT-024 (uncertain, disagree), PMT-025 (uncertain, disagree), PMT-026 (uncertain, disagree), PMT-027 (uncertain, disagree), PMT-028 (uncertain, disagree), PMT-029 (uncertain, disagree), PMT-030 (uncertain, disagree), PMT-031 (uncertain, disagree), PMT-032 (uncertain, disagree), PV-011 (uncertain, disagree), PV-012 (uncertain, disagree), NAME-006 (documented, blocked), NAME-007 (documented, blocked), NAME-008 (documented, blocked), NAME-036 (documented, blocked), SMALL-008 (uncertain, disagree), SORT-015 (uncertain, disagree), SORT-021 (uncertain, disagree), SORT-022 (uncertain, disagree), UNIQUE-017 (uncertain, disagree), UNIQUE-018 (uncertain, disagree), UNIQUE-019 (uncertain, disagree), XNPV-005 (uncertain, disagree), XNPV-006 (uncertain, disagree), XNPV-007 (uncertain, disagree), XNPV-008 (uncertain, disagree), XNPV-009 (uncertain, disagree), XNPV-010 (uncertain, disagree), XNPV-011 (uncertain, disagree), XNPV-012 (uncertain, disagree), XNPV-013 (uncertain, disagree), XNPV-014 (uncertain, disagree), XNPV-015 (uncertain, disagree), XNPV-017 (uncertain, disagree).
