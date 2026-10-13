## The cases ExSheet decides money by: NPV-001, NPV-002 and every PMT case

| Case | Source before | Formula | Outcome | Excel | Expected |
|---|---|---|---|---|---|
| NPV-001 | documented | `=NPV(10%,-10000,3000,4200,6800)` | **disagree** | Value2 `1188.4434123352207` (number), text `$1,188.44 ` | `{"value2": 1188.4434123352216, "digits": 12}` |
| NPV-002 | documented | `=NPV(A1,A3:A7)+A2` | **disagree** | Value2 `1922.0615549323702` (number), text `$1,922.06 ` | `{"value2": 1922.061554932363, "digits": 12}` |
| PMT-001 | observed | `=PMT(8%/12,10,10000)` | **agree** | Value2 `-1037.0320893591522` (number), text `($1,037.03)` | `{"value2": -1037.0320893591522}` |
| PMT-002 | observed | `=PMT(8%/12,10,10000,0,1)` | **agree** | Value2 `-1030.1643271779658` (number), text `($1,030.16)` | `{"value2": -1030.1643271779658}` |
| PMT-003 | observed | `=PMT(6%/12,18*12,0,50000)` | **agree** | Value2 `-129.08116086799092` (number), text `($129.08)` | `{"value2": -129.08116086799092}` |
| PMT-004 | observed | `=PMT(0,10,1000)` | **agree** | Value2 `-100` (number), text `($100.00)` | `{"value2": -100}` |
| PMT-005 | observed | `=PMT(0.1,0,1000)` | **agree** | Value2 `"#NUM!"` (error), text `#NUM!` | `{"value2": "#NUM!"}` |
| PMT-006 | observed | `=PMT(C2,10,1000)` | **agree** | Value2 `"#DIV/0!"` (error), text `#DIV/0!` | `{"value2": "#DIV/0!"}` |
| PMT-007 | observed | `=PMT(A6,10,1000)` | **agree** | Value2 `"#VALUE!"` (error), text `#VALUE!` | `{"value2": "#VALUE!"}` |
| PMT-008 | observed | `=PMT(0.1,10,1000,0,2)` | **agree** | Value2 `-147.95035898410148` (number), text `($147.95)` | `{"value2": -147.95035898410148}` |
| PMT-009 | uncertain | `=PMT(0.1,10,1000,0,0.5)` | **disagree** | Value2 `-147.95035898410148` (number), text `($147.95)` | `{"value2": "#VALUE!"}` |
| PMT-010 | uncertain | `=PMT(0.1,10,1000,0,-1)` | **agree** | Value2 `-147.95035898410148` (number), text `($147.95)` | `{"value2": -147.95035898410148}` |
| PMT-011 | uncertain | `=PMT(8%/12,10.5,10000)` | **disagree** | Value2 `-989.2729401451879` (number), text `($989.27)` | `{"value2": "#VALUE!"}` |
| PMT-012 | uncertain | `=PMT(8%/12,-10,10000)` | **disagree** | Value2 `970.3654226924854` (number), text `$970.37 ` | `{"value2": "#VALUE!"}` |
| PMT-013 | uncertain | `=PMT(0.05/12,36,250000,-5000)` | **disagree** | Value2 `-7363.7031239763755` (number), text `($7,363.70)` | `{"value2": -7363.703123976373}` |
| PMT-014 | uncertain | `=PMT(0.0725/12,12,250000,0,1)` | **disagree** | Value2 `-21530.430018725485` (number), text `($21,530.43)` | `{"value2": -21530.430018725492}` |
| PMT-015 | uncertain | `=PMT(0.05/12,12,10000,0,1)` | **disagree** | Value2 `-852.5226402171` (number), text `($852.52)` | `{"value2": -852.5226402170998}` |
| PMT-016 | uncertain | `=PMT(0.045/12,12,250000,-5000)` | **disagree** | Value2 `-20936.487788661154` (number), text `($20,936.49)` | `{"value2": -20936.48778866115}` |
| PMT-017 | uncertain | `=PMT(1/10000000,12,1000)` | **agree** | Value2 `-83.33338750000993` (number), text `($83.33)` | `{"value2": -83.33338750000993}` |
| PMT-018 | uncertain | `=PMT(0.0365/365,365,10000)` | **disagree** | Value2 `-27.901671561224465` (number), text `($27.90)` | `{"value2": -27.90167156122446}` |

## Every disagreement

| Case | File | Source before | Formula | Excel | Expected (`expect` or `excelExpect`) | What differs |
|---|---|---|---|---|---|---|
| AGGREGATE-060 | `aggregate.json` | uncertain | `=AGGREGATE(15,6,A1:A11,1.2)` | Value2 `30` (number), text `30` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel 30 (number) |
| AGGREGATE-061 | `aggregate.json` | uncertain | `=AGGREGATE(14,6,"5",1)` | Value2 `"#NUM!"` (error), text `#NUM!` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| EDATE-010 | `edate.json` | uncertain | `=EDATE(60,1)` | Value2 `89` (number), text `89` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel 89 (number) |
| EOMONTH-010 | `eomonth.json` | uncertain | `=EOMONTH(0,1)` | Value2 `59` (number), text `59` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel 59 (number) |
| FV-006 | `fv.json` | uncertain | `=FV(0.06/12,10.5,-200)` | Value2 `2150.588229777517` (number), text `$2,150.59 ` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel 2150.588229777517 (number) |
| FV-007 | `fv.json` | uncertain | `=FV(0.06/12,-10,-200)` | Value2 `-1946.0823721571962` (number), text `($1,946.08)` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel -1946.0823721571962 (number) |
| FV-009 | `fv.json` | uncertain | `=FV(0.05/12,12,-150,0,1)` | Value2 `1849.5026084246633` (number), text `$1,849.50 ` | `{"value2": 1849.502608424663}` (expect) | value2: expected 1849.502608424663, Excel 1849.5026084246633 (number) |
| LARGE-011 | `large.json` | uncertain | `=LARGE(A1:B5,0.5)` | Value2 `"#NUM!"` (error), text `#NUM!` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| MOD-017 | `mod.json` | uncertain | `=MOD(123456789012,0.7)` | Value2 `0.5000078322611261` (number), text `0.500007832` | `{"value2": 0.5000152587890625}` (expect) | value2: expected 0.5000152587890625, Excel 0.50000783226112611 (number) |
| NPV-001 | `npv.json` | documented | `=NPV(10%,-10000,3000,4200,6800)` | Value2 `1188.4434123352207` (number), text `$1,188.44 ` | `{"value2": 1188.4434123352216, "digits": 12}` (expect) | value2: expected 1188.4434123352216, Excel 1188.4434123352207 (number) |
| NPV-002 | `npv.json` | documented | `=NPV(A1,A3:A7)+A2` | Value2 `1922.0615549323702` (number), text `$1,922.06 ` | `{"value2": 1922.061554932363, "digits": 12}` (expect) | value2: expected 1922.061554932363, Excel 1922.0615549323702 (number) |
| NPV-005 | `npv.json` | uncertain | `=NPV(0.08,8000,9200,10000,12000,14500)` | Value2 `41922.06155493237` (number), text `$41,922.06 ` | `{"value2": 41922.06155493236, "digits": 12}` (expect) | value2: expected 41922.06155493236, Excel 41922.06155493237 (number) |
| NPV-006 | `npv.json` | uncertain | `=NPV(A1,A3:A7)` | Value2 `41922.06155493237` (number), text `$41,922.06 ` | `{"value2": 41922.06155493236, "digits": 12}` (expect) | value2: expected 41922.06155493236, Excel 41922.06155493237 (number) |
| NPV-007 | `npv.json` | uncertain | `=NPV(0.1,A1:A4)` | Value2 `1188.4434123352207` (number), text `$1,188.44 ` | `{"value2": 1188.4434123352216, "digits": 12}` (expect) | value2: expected 1188.4434123352216, Excel 1188.4434123352207 (number) |
| NPV-008 | `npv.json` | uncertain | `=NPV(A1,-10000,3000,4200,6800)` | Value2 `1188.4434123352207` (number), text `$1,188.44 ` | `{"value2": 1188.4434123352216, "digits": 12}` (expect) | value2: expected 1188.4434123352216, Excel 1188.4434123352207 (number) |
| PMT-009 | `pmt.json` | uncertain | `=PMT(0.1,10,1000,0,0.5)` | Value2 `-147.95035898410148` (number), text `($147.95)` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel -147.95035898410148 (number) |
| PMT-011 | `pmt.json` | uncertain | `=PMT(8%/12,10.5,10000)` | Value2 `-989.2729401451879` (number), text `($989.27)` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel -989.27294014518793 (number) |
| PMT-012 | `pmt.json` | uncertain | `=PMT(8%/12,-10,10000)` | Value2 `970.3654226924854` (number), text `$970.37 ` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel 970.36542269248537 (number) |
| PMT-013 | `pmt.json` | uncertain | `=PMT(0.05/12,36,250000,-5000)` | Value2 `-7363.7031239763755` (number), text `($7,363.70)` | `{"value2": -7363.703123976373}` (expect) | value2: expected -7363.703123976373, Excel -7363.7031239763755 (number) |
| PMT-014 | `pmt.json` | uncertain | `=PMT(0.0725/12,12,250000,0,1)` | Value2 `-21530.430018725485` (number), text `($21,530.43)` | `{"value2": -21530.430018725492}` (expect) | value2: expected -21530.430018725492, Excel -21530.430018725485 (number) |
| PMT-015 | `pmt.json` | uncertain | `=PMT(0.05/12,12,10000,0,1)` | Value2 `-852.5226402171` (number), text `($852.52)` | `{"value2": -852.5226402170998}` (expect) | value2: expected -852.5226402170998, Excel -852.5226402171 (number) |
| PMT-016 | `pmt.json` | uncertain | `=PMT(0.045/12,12,250000,-5000)` | Value2 `-20936.487788661154` (number), text `($20,936.49)` | `{"value2": -20936.48778866115}` (expect) | value2: expected -20936.48778866115, Excel -20936.487788661154 (number) |
| PMT-018 | `pmt.json` | uncertain | `=PMT(0.0365/365,365,10000)` | Value2 `-27.901671561224465` (number), text `($27.90)` | `{"value2": -27.90167156122446}` (expect) | value2: expected -27.90167156122446, Excel -27.901671561224465 (number) |
| PV-005 | `pv.json` | uncertain | `=PV(0.05,10.5,-100)` | Value2 `801.7640221710038` (number), text `$801.76 ` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel 801.76402217100383 (number) |
| PV-007 | `pv.json` | uncertain | `=PV(0.05/12,12,-150,0,1)` | Value2 `1759.484064397425` (number), text `$1,759.48 ` | `{"value2": 1759.4840643974248}` (expect) | value2: expected 1759.4840643974248, Excel 1759.4840643974251 (number) |
| SMALL-004 | `small.json` | uncertain | `=SMALL(A1:A9,1.5)` | Value2 `2` (number), text `2` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel 2 (number) |
| SMALL-005 | `small.json` | uncertain | `=SMALL(A1:A9,1.2)` | Value2 `2` (number), text `2` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel 2 (number) |
| TEXTFN-025 | `text.json` | uncertain | `=TEXT(-1/3,"General")` | Value2 `"-0.333333333"` (text), text `-0.333333333` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel "-0.333333333" (text) |
| TEXTFN-028 | `text.json` | uncertain | `=TEXT(1/3,"@")` | Value2 `"0.333333333"` (text), text `0.333333333` | `{"value2": "#VALUE!"}` (expect) | value2: expected "#VALUE!", Excel "0.333333333" (text) |

## Blocked, and why

| Case | File | Source | Why |
|---|---|---|---|
| CW-013 | `column-widths.json` | observed | Excel's undo does not reach changes made through COM |
| CW-014 | `column-widths.json` | observed | Excel's undo does not reach changes made through COM |
| CW-026 | `column-widths.json` | observed | A value set through COM never widens a column, and Excel's undo does not reach changes made through COM: asked by hand (verification/2026-10-11-windows-17, b10) |
| NAME-006 | `sheet-names.json` | documented | Excel reads a qualifier naming no sheet as an external workbook and may open a file dialog; ask by hand |
| NAME-007 | `sheet-names.json` | documented | Excel reads a qualifier naming no sheet as an external workbook and may open a file dialog; ask by hand |
| NAME-008 | `sheet-names.json` | documented | Excel reads a qualifier naming no sheet as an external workbook and may open a file dialog; ask by hand |
| NAME-036 | `sheet-names.json` | documented | Excel's undo does not reach changes made through COM; ask by hand |

## Recorded (Excel answers, the engine refuses by decision; nothing is compared)

| Case | File | Excel | The engine | Decision |
|---|---|---|---|---|
| INDIRECT-036 | `indirect.json` | Value2 `45` (number), text `45` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-10, the seventeenth Windows run): R1C1 text in a form no run has observed, relative or naming whole rows or columns, is #VALUE! until one observes it |
| INDIRECT-037 | `indirect.json` | Value2 `45` (number), text `45` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-10, the seventeenth Windows run): R1C1 text in a form no run has observed, relative or naming whole rows or columns, is #VALUE! until one observes it |
| INDIRECT-038 | `indirect.json` | Value2 `118.333` (number), text `118.333` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-10, the seventeenth Windows run): R1C1 text in a form no run has observed, relative or naming whole rows or columns, is #VALUE! until one observes it |
| INDIRECT-043 | `indirect.json` | Value2 `250.5` (number), text `250.5` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-10): structured reference text, a table's bare name included, is #VALUE! until a Windows run observes it |
| SORT-012 | `sort.json` | Value2 `0` (number), text `0` | `{"value2": null}` | ADR-0202, ADR-0047: a blank among SORT's keys is refused with #VALUE! until Excel is asked where it sorts; recorded, so that a run reads Excel's answer |
| SORT-013 | `sort.json` | Value2 `7` (number), text `7` | `{"value2": "#VALUE!"}` | ADR-0202, ADR-0047: a blank among SORT's keys is refused with #VALUE! until Excel is asked where it sorts; recorded, so that a run reads Excel's answer |
| SORT-014 | `sort.json` | Value2 `"a"` (text), text `a` | `{"value2": "#VALUE!"}` | ADR-0202, ADR-0047: a blank among SORT's keys is refused with #VALUE! until Excel is asked where it sorts; recorded, so that a run reads Excel's answer |
| UNIQUE-010 | `unique.json` | Value2 `0` (number), text `0` | `{"value2": null}` | ADR-0202, ADR-0047: a blank in UNIQUE's array is refused with #VALUE! until Excel is asked whether it is one of a kind with 0; recorded, so that a run reads Excel's answer |
| UNIQUE-011 | `unique.json` | Value2 `2` (number), text `2` | `{"value2": "#VALUE!"}` | ADR-0202, ADR-0047: a blank in UNIQUE's array is refused with #VALUE! until Excel is asked whether it is one of a kind with 0; recorded, so that a run reads Excel's answer |

## Every case the pass asked, by file

Each case once, by its pass of record. A case promoted is one whose `source` this pass rewrote to `observed`.

| File | agreed | of which promoted | disagreed | blocked | recorded |
|---|---|---|---|---|---|
| `aggregate.json` | AGGREGATE-001, AGGREGATE-002, AGGREGATE-003, AGGREGATE-004, AGGREGATE-005, AGGREGATE-006, AGGREGATE-007, AGGREGATE-008, AGGREGATE-009, AGGREGATE-010, AGGREGATE-011, AGGREGATE-012, AGGREGATE-013, AGGREGATE-014, AGGREGATE-015, AGGREGATE-016, AGGREGATE-017, AGGREGATE-018, AGGREGATE-019, AGGREGATE-020, AGGREGATE-021, AGGREGATE-022, AGGREGATE-023, AGGREGATE-024, AGGREGATE-025, AGGREGATE-026, AGGREGATE-027, AGGREGATE-028, AGGREGATE-029, AGGREGATE-030, AGGREGATE-031, AGGREGATE-032, AGGREGATE-033, AGGREGATE-034, AGGREGATE-035, AGGREGATE-036, AGGREGATE-037, AGGREGATE-038, AGGREGATE-039, AGGREGATE-040, AGGREGATE-041, AGGREGATE-042, AGGREGATE-043, AGGREGATE-044, AGGREGATE-045, AGGREGATE-046, AGGREGATE-047, AGGREGATE-048, AGGREGATE-049, AGGREGATE-050, AGGREGATE-051, AGGREGATE-052, AGGREGATE-053, AGGREGATE-054, AGGREGATE-055, AGGREGATE-056, AGGREGATE-057, AGGREGATE-058, AGGREGATE-059 | AGGREGATE-055, AGGREGATE-056, AGGREGATE-057, AGGREGATE-058, AGGREGATE-059 | AGGREGATE-060, AGGREGATE-061 | – | – |
| `and.json` | AND-001, AND-002, AND-003, AND-004, AND-005, AND-006, AND-007, AND-008, AND-009, AND-010, AND-011, AND-012, AND-013, AND-014, AND-015 | AND-014, AND-015 | – | – | – |
| `column-widths.json` | CW-001, CW-002, CW-003, CW-004, CW-005, CW-006, CW-007, CW-008, CW-009, CW-010, CW-011, CW-012, CW-015, CW-016, CW-017, CW-018, CW-019, CW-020, CW-021, CW-022, CW-023, CW-024, CW-025, CW-027, CW-028, CW-029, CW-030, CW-031, CW-032 | CW-030, CW-031, CW-032 | – | CW-013, CW-014, CW-026 | – |
| `cos.json` | COS-001, COS-002, COS-003, COS-004, COS-005, COS-006 | COS-006 | – | – | – |
| `cot.json` | COT-001, COT-002, COT-003, COT-004, COT-005, COT-006, COT-007, COT-008 | COT-006, COT-007, COT-008 | – | – | – |
| `coth.json` | COTH-001, COTH-002, COTH-003, COTH-004, COTH-005, COTH-006 | COTH-004, COTH-005, COTH-006 | – | – | – |
| `edate.json` | EDATE-001, EDATE-002, EDATE-003, EDATE-004, EDATE-005, EDATE-006, EDATE-007, EDATE-008, EDATE-009, EDATE-011, EDATE-012 | EDATE-011, EDATE-012 | EDATE-010 | – | – |
| `eomonth.json` | EOMONTH-001, EOMONTH-002, EOMONTH-003, EOMONTH-004, EOMONTH-005, EOMONTH-006, EOMONTH-007, EOMONTH-008, EOMONTH-009, EOMONTH-011 | EOMONTH-011 | EOMONTH-010 | – | – |
| `fv.json` | FV-001, FV-002, FV-003, FV-004, FV-005, FV-008, FV-010 | FV-005, FV-008, FV-010 | FV-006, FV-007, FV-009 | – | – |
| `index.json` | INDEX-001, INDEX-002, INDEX-003, INDEX-004, INDEX-005, INDEX-006, INDEX-007, INDEX-008, INDEX-009, INDEX-010, INDEX-011, INDEX-012, INDEX-013, INDEX-014, INDEX-015, INDEX-016, INDEX-017, INDEX-018 | INDEX-014, INDEX-015, INDEX-016, INDEX-017, INDEX-018 | – | – | – |
| `indirect.json` | INDIRECT-001, INDIRECT-002, INDIRECT-003, INDIRECT-004, INDIRECT-005, INDIRECT-006, INDIRECT-007, INDIRECT-008, INDIRECT-009, INDIRECT-010, INDIRECT-011, INDIRECT-012, INDIRECT-013, INDIRECT-014, INDIRECT-015, INDIRECT-016, INDIRECT-017, INDIRECT-018, INDIRECT-019, INDIRECT-020, INDIRECT-021, INDIRECT-022, INDIRECT-023, INDIRECT-024, INDIRECT-025, INDIRECT-026, INDIRECT-027, INDIRECT-028, INDIRECT-029, INDIRECT-030, INDIRECT-031, INDIRECT-032, INDIRECT-033, INDIRECT-034, INDIRECT-035, INDIRECT-039, INDIRECT-040, INDIRECT-041, INDIRECT-042 | INDIRECT-030, INDIRECT-031, INDIRECT-032, INDIRECT-033, INDIRECT-034, INDIRECT-035, INDIRECT-039, INDIRECT-040, INDIRECT-041, INDIRECT-042 | – | – | INDIRECT-036, INDIRECT-037, INDIRECT-038, INDIRECT-043 |
| `large.json` | LARGE-001, LARGE-002, LARGE-003, LARGE-004, LARGE-005, LARGE-006, LARGE-007, LARGE-008, LARGE-009, LARGE-010, LARGE-012 | LARGE-008, LARGE-009, LARGE-010, LARGE-012 | LARGE-011 | – | – |
| `mod.json` | MOD-001, MOD-002, MOD-003, MOD-004, MOD-005, MOD-006, MOD-007, MOD-008, MOD-009, MOD-010, MOD-011, MOD-012, MOD-013, MOD-014, MOD-015, MOD-016 | MOD-010, MOD-011, MOD-012, MOD-013, MOD-014, MOD-015, MOD-016 | MOD-017 | – | – |
| `networkdays.json` | NETWORKDAYS-001, NETWORKDAYS-002, NETWORKDAYS-003, NETWORKDAYS-004, NETWORKDAYS-005, NETWORKDAYS-006, NETWORKDAYS-007, NETWORKDAYS-008 | NETWORKDAYS-006, NETWORKDAYS-007, NETWORKDAYS-008 | – | – | – |
| `npv.json` | NPV-003, NPV-004 | – | NPV-001, NPV-002, NPV-005, NPV-006, NPV-007, NPV-008 | – | – |
| `or.json` | OR-001, OR-002, OR-003, OR-004, OR-005, OR-006, OR-007, OR-008, OR-009, OR-010 | OR-009, OR-010 | – | – | – |
| `pmt.json` | PMT-001, PMT-002, PMT-003, PMT-004, PMT-005, PMT-006, PMT-007, PMT-008, PMT-010, PMT-017 | PMT-010, PMT-017 | PMT-009, PMT-011, PMT-012, PMT-013, PMT-014, PMT-015, PMT-016, PMT-018 | – | – |
| `pv.json` | PV-001, PV-002, PV-003, PV-004, PV-006 | PV-004, PV-006 | PV-005, PV-007 | – | – |
| `sec.json` | SEC-001, SEC-002, SEC-003, SEC-004, SEC-005 | SEC-005 | – | – | – |
| `sheet-names.json` | NAME-001, NAME-002, NAME-003, NAME-004, NAME-005, NAME-009, NAME-010, NAME-011, NAME-012, NAME-013, NAME-014, NAME-015, NAME-016, NAME-017, NAME-018, NAME-019, NAME-020, NAME-021, NAME-022, NAME-023, NAME-024, NAME-025, NAME-026, NAME-027, NAME-028, NAME-029, NAME-030, NAME-031, NAME-032, NAME-033, NAME-034, NAME-035, NAME-037 | – | – | NAME-006, NAME-007, NAME-008, NAME-036 | – |
| `sin.json` | SIN-001, SIN-002, SIN-003, SIN-004, SIN-005, SIN-006, SIN-007, SIN-008 | SIN-008 | – | – | – |
| `small.json` | SMALL-001, SMALL-002, SMALL-003, SMALL-006 | SMALL-006 | SMALL-004, SMALL-005 | – | – |
| `sort.json` | SORT-001, SORT-002, SORT-003, SORT-004, SORT-005, SORT-006, SORT-007, SORT-008, SORT-009, SORT-010, SORT-011 | – | – | – | SORT-012, SORT-013, SORT-014 |
| `subtotal.json` | SUBTOTAL-001, SUBTOTAL-002, SUBTOTAL-003, SUBTOTAL-004, SUBTOTAL-005, SUBTOTAL-006, SUBTOTAL-007, SUBTOTAL-008, SUBTOTAL-009, SUBTOTAL-010, SUBTOTAL-011, SUBTOTAL-012, SUBTOTAL-013, SUBTOTAL-014, SUBTOTAL-015, SUBTOTAL-016, SUBTOTAL-017, SUBTOTAL-018, SUBTOTAL-019, SUBTOTAL-020, SUBTOTAL-021, SUBTOTAL-022, SUBTOTAL-023, SUBTOTAL-024, SUBTOTAL-025, SUBTOTAL-026, SUBTOTAL-027, SUBTOTAL-028, SUBTOTAL-029, SUBTOTAL-030, SUBTOTAL-031, SUBTOTAL-032, SUBTOTAL-033, SUBTOTAL-034, SUBTOTAL-035, SUBTOTAL-036, SUBTOTAL-037, SUBTOTAL-038, SUBTOTAL-039, SUBTOTAL-040, SUBTOTAL-041, SUBTOTAL-042, SUBTOTAL-043, SUBTOTAL-044 | SUBTOTAL-039, SUBTOTAL-040, SUBTOTAL-041, SUBTOTAL-042, SUBTOTAL-043, SUBTOTAL-044 | – | – | – |
| `tan.json` | TAN-001, TAN-002, TAN-003, TAN-004, TAN-005, TAN-006, TAN-007 | TAN-006, TAN-007 | – | – | – |
| `text.json` | TEXTFN-001, TEXTFN-002, TEXTFN-003, TEXTFN-004, TEXTFN-005, TEXTFN-006, TEXTFN-007, TEXTFN-008, TEXTFN-009, TEXTFN-010, TEXTFN-011, TEXTFN-012, TEXTFN-013, TEXTFN-014, TEXTFN-015, TEXTFN-016, TEXTFN-017, TEXTFN-018, TEXTFN-019, TEXTFN-020, TEXTFN-021, TEXTFN-022, TEXTFN-023, TEXTFN-024, TEXTFN-026, TEXTFN-027 | TEXTFN-023, TEXTFN-024, TEXTFN-026, TEXTFN-027 | TEXTFN-025, TEXTFN-028 | – | – |
| `unique.json` | UNIQUE-001, UNIQUE-002, UNIQUE-003, UNIQUE-004, UNIQUE-005, UNIQUE-006, UNIQUE-007, UNIQUE-008, UNIQUE-009 | – | – | – | UNIQUE-010, UNIQUE-011 |
| `workday.json` | WORKDAY-001, WORKDAY-002, WORKDAY-003, WORKDAY-004, WORKDAY-005, WORKDAY-006, WORKDAY-007, WORKDAY-008, WORKDAY-009 | WORKDAY-006, WORKDAY-007, WORKDAY-008, WORKDAY-009 | – | – | – |
| `xnpv.json` | XNPV-001, XNPV-002, XNPV-003, XNPV-004 | XNPV-004 | – | – | – |
| `xor.json` | XOR-001, XOR-002, XOR-003, XOR-004, XOR-005, XOR-006 | XOR-006 | – | – | – |

## The passes

| Results file | Regional format | How | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|
| `results-2026-10-12-area-com-en-US.json` | en-US | COM | 422 | 29 | 21 | 9 |
| `results-2026-10-12-area-keys-en-US.json` | en-US | keys | 11 | 0 | 0 | 0 |
| `results-2026-10-12-area-com-de-DE.json` | de-DE | COM | 2 | 0 | 0 | 0 |
| `results-2026-10-12-area-keys-de-DE.json` | de-DE | keys | 2 | 0 | 0 | 0 |
| `results-2026-10-12-area-com-ja-JP.json` | ja-JP | COM | 1 | 0 | 0 | 0 |
| `results-2026-10-12-area-keys-ja-JP.json` | ja-JP | keys | 1 | 0 | 0 | 0 |

Sources rewritten by `-Update`: documented → observed: 1, uncertain → observed: 78.

Still not `observed` after the pass (42): AGGREGATE-060 (uncertain, disagree), AGGREGATE-061 (uncertain, disagree), EDATE-010 (uncertain, disagree), EOMONTH-010 (uncertain, disagree), FV-006 (uncertain, disagree), FV-007 (uncertain, disagree), FV-009 (uncertain, disagree), INDIRECT-036 (uncertain, recorded), INDIRECT-037 (uncertain, recorded), INDIRECT-038 (uncertain, recorded), INDIRECT-043 (uncertain, recorded), LARGE-011 (uncertain, disagree), MOD-017 (uncertain, disagree), NPV-001 (documented, disagree), NPV-002 (documented, disagree), NPV-005 (uncertain, disagree), NPV-006 (uncertain, disagree), NPV-007 (uncertain, disagree), NPV-008 (uncertain, disagree), PMT-009 (uncertain, disagree), PMT-011 (uncertain, disagree), PMT-012 (uncertain, disagree), PMT-013 (uncertain, disagree), PMT-014 (uncertain, disagree), PMT-015 (uncertain, disagree), PMT-016 (uncertain, disagree), PMT-018 (uncertain, disagree), PV-005 (uncertain, disagree), PV-007 (uncertain, disagree), NAME-006 (documented, blocked), NAME-007 (documented, blocked), NAME-008 (documented, blocked), NAME-036 (documented, blocked), SMALL-004 (uncertain, disagree), SMALL-005 (uncertain, disagree), SORT-012 (uncertain, recorded), SORT-013 (uncertain, recorded), SORT-014 (uncertain, recorded), TEXTFN-025 (uncertain, disagree), TEXTFN-028 (uncertain, disagree), UNIQUE-010 (uncertain, recorded), UNIQUE-011 (uncertain, recorded).
