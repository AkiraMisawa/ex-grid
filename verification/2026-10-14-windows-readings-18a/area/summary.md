## Every disagreement

| Case | File | Source before | Formula | Excel | Excel's text | Expected | Units in the last place | What differs |
|---|---|---|---|---|---|---|---|---|
| DATEDIF-048 | `datedif.json` | uncertain | `=DATEDIF(-0.5,1,"D")` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| EDATE-018 | `edate.json` | uncertain | `=EDATE(0,2)` | 60 | `60` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 60 (number) |
| EDATE-019 | `edate.json` | uncertain | `=EDATE(0,0)` | 0 | `0` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0 (number) |
| EDATE-020 | `edate.json` | uncertain | `=EDATE(0,97200)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| FILTER-014 | `filter.json` | uncertain | `=AVERAGE(FILTER(Q1:Q2,H1:H2>0))` | 7 | `7` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 7 (number) |
| HOUR-005 | `hour.json` | uncertain | `=HOUR(-0.5)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| INDIRECT-075 | `indirect.json` | uncertain | `=SUM(INDIRECT("R2C2:C3",FALSE))` | 1.333 | `1.333` | `#REF!` |  | value2: expected "#REF!", Excel 1.333 (number) |
| INDIRECT-076 | `indirect.json` | uncertain | `=SUM(INDIRECT("R3:R2C2",FALSE))` | 46.332999999999998 | `46.333` | `#REF!` |  | value2: expected "#REF!", Excel 46.333 (number) |
| MINUTE-003 | `minute.json` | uncertain | `=MINUTE(-0.5)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| MOD-033 | `mod.json` | uncertain | `=MOD(-0.00000000000000001,1)` | 0 | `0` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0 (number) |
| MOD-034 | `mod.json` | uncertain | `=MOD(-0.084474,2270)` | 2269.9155260000002 | `2269.915526` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2269.915526 (number) |
| MOD-035 | `mod.json` | uncertain | `=MOD(-0.008511,81200)` | 81199.991489000007 | `81199.99149` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 81199.991489 (number) |
| MOD-036 | `mod.json` | uncertain | `=MOD(0.071472,-276)` | -275.92852800000003 | `-275.928528` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -275.928528 (number) |
| MOD-039 | `mod.json` | uncertain | `=MOD(-0.024901,2350)` | 2349.9750989999998 | `2349.975099` | 2349.975099 | 0 | value2: expected 2349.975099, Excel 2349.9750989999998 (number) |
| PMT-034 | `pmt.json` | uncertain | `=PMT(-1,10,1000,500)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| PMT-035 | `pmt.json` | uncertain | `=PMT(-2,2,1000)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| PMT-036 | `pmt.json` | uncertain | `=PMT(-9,1/3,-1)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| PMT-037 | `pmt.json` | uncertain | `=PMT(-1,-10,1000)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| PMT-038 | `pmt.json` | uncertain | `=PMT(1,1100,1000)` | -1000 | `($1,000.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -1000 (number) |
| PMT-039 | `pmt.json` | uncertain | `=PMT(1,1020,1000)` | -1000 | `($1,000.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -1000 (number) |
| PMT-040 | `pmt.json` | uncertain | `=PMT(0.5,1740,1000)` | -500 | `($500.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -500 (number) |
| PMT-041 | `pmt.json` | uncertain | `=PMT(10000000000,30,1)` | -10000000000 | `($10,000,000,000.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -10000000000 (number) |
| PMT-042 | `pmt.json` | uncertain | `=PMT(-0.99,160,1000,500)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| PMT-043 | `pmt.json` | uncertain | ZZ1000, spilled by `=PMT(1E-320,0.5,1000)` in ZZ1000 | `None` | `None` | `#VALUE!` |  | Excel would not enter =PMT(1E-320,0.5,1000) (Exception from HRESULT: 0x800A03EC) |
| PMT-044 | `pmt.json` | uncertain | `=PMT(-1.64435447903749E-307,283.5,0.00001,0)` | -3.5273368606701946E-08 | `($0.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -3.5273368606701946E-08 (number) |
| PMT-045 | `pmt.json` | uncertain | `=PMT(-0.5,360,1000,500)` | -250 | `($250.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -250 (number) |
| PMT-046 | `pmt.json` | uncertain | `=PMT(0.42,0,1000,0,1)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| PMT-047 | `pmt.json` | uncertain | `=PMT(0.1,0.00000000000000001,1000,0,1)` | -9.5382351702337012E+19 | `($95,382,351,702,337,000,000.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -9.5382351702337012E+19 (number) |
| PMT-048 | `pmt.json` | uncertain | `=PMT(-0.999999,0.000000000001,1000,0,1)` | -7.238234126737118E+19 | `($72,382,341,267,371,200,000.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -7.238234126737118E+19 (number) |
| PMT-049 | `pmt.json` | uncertain | `=PMT(0.1,2,1000,-1210)` | 2.2737367544323207E-14 | `$0.00 ` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 2.2737367544323207E-14 (number) |
| PMT-050 | `pmt.json` | uncertain | `=PMT(0.1,0.00000000000000001,1000,-1000)` | -100 | `($100.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -100 (number) |
| PMT-051 | `pmt.json` | uncertain | `=PMT(-0.5,36.25,-150,0,0)` | 9.1774900130695183E-10 | `$0.00 ` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 9.1774900130695183E-10 (number) |
| PMT-052 | `pmt.json` | uncertain | `=PMT(1,1000,0,1000)` | 0 | `$0.00 ` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0 (number) |
| PMT-053 | `pmt.json` | uncertain | `=PMT(1000,2,1000,0,1)` | -999.00199600798419 | `($999.00)` | `#VALUE!` |  | value2: expected "#VALUE!", Excel -999.00199600798419 (number) |
| PV-013 | `pv.json` | uncertain | `=PV(-1,10,-100)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#DIV/0!" (error) |
| SECOND-003 | `second.json` | uncertain | `=SECOND(-0.5)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| SORT-026 | `sort.json` | uncertain | `=SORT(A1:A2)` | 7 | `7` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 7 (number) |
| SORT-028 | `sort.json` | uncertain | `=SORT(A1:A2,1,-1)` | `#N/A` | `#N/A` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#N/A" (error) |
| SORT-029 | `sort.json` | uncertain | `=SORT(A1:A2)` | `a` (text) | `a` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "a" (text) |
| SORT-030 | `sort.json` | uncertain | `=SORT(A1:A2)` | `True` | `TRUE` | `#VALUE!` |  | value2: expected "#VALUE!", Excel true (boolean) |
| SORT-031 | `sort.json` | uncertain | `=SORT(A1:A2)` | `#N/A` | `#N/A` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#N/A" (error) |
| SORT-032 | `sort.json` | uncertain | `=INDEX(SORT(A1:A3),2)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#DIV/0!" (error) |
| SORT-034 | `sort.json` | uncertain | `=COUNT(SORT(Q1:Q2))` | 1 | `1` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 1 (number) |
| SORT-035 | `sort.json` | uncertain | `=COUNTA(INDEX(SORT(Q1:Q2),2))` | 1 | `1` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 1 (number) |
| SORT-037 | `sort.json` | uncertain | `=SUM(SORT(A1:A4))` | 0 | `0` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 0 (number) |
| SORT-038 | `sort.json` | uncertain | `=SORT(FILTER(Q1:Q2,H1:H2>0))` | 7 | `7` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 7 (number) |
| SORT-039 | `sort.json` | uncertain | `=ISBLANK(INDEX(IF(TRUE,SORT(Q1:Q2)),2))` | `True` | `TRUE` | `#VALUE!` |  | value2: expected "#VALUE!", Excel true (boolean) |
| SORT-042 | `sort.json` | uncertain | `=SORT(G1:H3,1.5)` | 1 | `1` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 1 (number) |
| SORTBY-010 | `sortby.json` | uncertain | `=SORTBY(H1:H2,Z1:Z2)` | 10 | `10` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 10 (number) |
| TRANSPOSE-007 | `transpose.json` | uncertain | `=MIN(TRANSPOSE(Q1:Q2))` | 7 | `7` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 7 (number) |
| UNIQUE-021 | `unique.json` | uncertain | `=AVERAGE(UNIQUE(Q1:Q2))` | 7 | `7` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 7 (number) |
| UNIQUE-022 | `unique.json` | uncertain | `=MIN(UNIQUE(Q1:Q2))` | 7 | `7` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 7 (number) |
| UNIQUE-023 | `unique.json` | uncertain | `=COUNTA(UNIQUE(Q2))` | 1 | `1` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 1 (number) |
| XNPV-018 | `xnpv.json` | uncertain | `=XNPV(0.1,A1:A2,B1:B2)` | 1.1368683772161603E-13 | `1.13687E-13` | `#VALUE!` |  | value2: expected "#VALUE!", Excel 1.1368683772161603E-13 (number) |
| XNPV-019 | `xnpv.json` | uncertain | `=XNPV(0,A1:A5,B1:B5)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-021 | `xnpv.json` | uncertain | `=XNPV(-0.5,A1:A2,B1:B2)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-022 | `xnpv.json` | uncertain | `=XNPV(-0.5,A1:A2,B1:B2)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-023 | `xnpv.json` | uncertain | `=XNPV(0,A1:A2,B1:B2)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-024 | `xnpv.json` | uncertain | `=XNPV(0.09,A1:A3,B1:B2)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |
| XNPV-025 | `xnpv.json` | uncertain | `=XNPV(0.09,A1:A2,B1:B2)` | `#N/A` | `#N/A` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#N/A" (error) |
| XNPV-026 | `xnpv.json` | uncertain | `=XNPV(0.09,A1:A2,B1:B2)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#DIV/0!" (error) |
| XNPV-027 | `xnpv.json` | uncertain | `=XNPV(1/0,A1:A2,B1:B2)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#DIV/0!" (error) |
| XNPV-028 | `xnpv.json` | uncertain | `=XNPV(1/0,A1:A3,B1:B2)` | `#DIV/0!` | `#DIV/0!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#DIV/0!" (error) |
| XNPV-029 | `xnpv.json` | uncertain | `=XNPV(0.09,A1:A3,B1:B3)` | `#N/A` | `#N/A` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#N/A" (error) |
| XNPV-030 | `xnpv.json` | uncertain | `=XNPV(0.09,A1:A2,B1:B2)` | `#N/A` | `#N/A` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#N/A" (error) |
| YEARFRAC-058 | `yearfrac.json` | uncertain | `=YEARFRAC(-0.5,1)` | `#NUM!` | `#NUM!` | `#VALUE!` |  | value2: expected "#VALUE!", Excel "#NUM!" (error) |

## Blocked, and why

| Case | File | Source | Why |
|---|---|---|---|
| DATEDIF-001 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-002 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-003 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-004 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-005 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-006 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-007 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-008 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-009 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-010 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-011 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-012 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-013 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-014 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-015 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-016 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-017 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-018 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-019 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-020 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-021 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-022 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-023 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-024 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-025 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-026 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-027 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-028 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-029 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-030 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-031 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-032 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-033 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-034 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-035 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-036 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-037 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-038 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-039 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-040 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-041 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-042 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-043 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-044 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-045 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-046 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| DATEDIF-047 | `datedif.json` | observed | needs en-GB; this machine's regional format is en-US |
| ERRORTYPE-013 | `error-type.json` | observed | Range.FormulaLocal keeps this as text, but typed with real keys Excel reads the Error Value (verification/2026-10-11-windows-17); ask by keys |
| ERRORTYPE-016 | `error-type.json` | observed | Range.FormulaLocal keeps this as text, but typed with real keys Excel reads the Error Value (verification/2026-10-11-windows-17); ask by keys |
| ERRORTYPE-017 | `error-type.json` | observed | Range.FormulaLocal keeps this as text, but typed with real keys Excel reads the Error Value (verification/2026-10-11-windows-17); ask by keys |
| ERRORTYPE-018 | `error-type.json` | observed | Range.FormulaLocal keeps this as text, but typed with real keys Excel reads the Error Value (verification/2026-10-11-windows-17); ask by keys |
| ERRORTYPE-019 | `error-type.json` | observed | Range.FormulaLocal keeps this as text, but typed with real keys Excel reads the Error Value (verification/2026-10-11-windows-17); ask by keys |
| TABLE-005 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-006 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-007 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-008 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-009 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| TABLE-010 | `linked-tables.json` | documented | Excel has no Linked Table waiting for its data; nothing to ask |
| YEARFRAC-001 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-002 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-003 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-004 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-005 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-006 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-007 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-008 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-009 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-010 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-011 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-012 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-013 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-014 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-015 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-016 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-017 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-018 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-019 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-020 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-021 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-022 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-023 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-024 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-025 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-026 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-027 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-028 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-029 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-030 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-031 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-032 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-033 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-034 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-035 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-036 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-037 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-038 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-039 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-040 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-041 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-042 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-043 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-044 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-045 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-046 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-047 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-048 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-049 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-050 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-051 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-052 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-053 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-054 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-055 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-056 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |
| YEARFRAC-057 | `yearfrac.json` | observed | needs en-GB; this machine's regional format is en-US |

## Recorded (the engine refuses by decision; Excel's answer is read, not compared)

| Case | File | Formula | Excel | Excel's text | The engine | Decision |
|---|---|---|---|---|---|---|
| AGGREGATE-069 | `aggregate.json` | `=AGGREGATE(9,6,B1:B2)` | 7 | `7` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| COUNT-009 | `count.json` | `=COUNT(B1:B2)` | 2 | `2` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| COUNTA-009 | `counta.json` | `=COUNTA(B1:B2)` | 2 | `2` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| ERRORTYPE-022 | `error-type.json` | `=ERROR.TYPE(B1)` | `#N/A` | `#N/A` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| IFERROR-017 | `iferror.json` | `=IFERROR(LEN(A1:A3),0)` | 1 | `1` | `{"value2": "#VALUE!"}` | ADR-0125: functions are not lifted over arrays yet; LEN given a range is #VALUE!, where Ex… |
| IFERROR-018 | `iferror.json` | `=IFERROR(AGGREGATE(16,6,A1:A11,0.5),0)` | 34 | `34` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): the For… |
| IFERROR-019 | `iferror.json` | `=IFERROR(B1,0)` | 6 | `6` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| IFERROR-020 | `iferror.json` | `=IFERROR(C1,1)` | 7.1000000000000005 | `7.1` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| IFNA-008 | `ifna.json` | `=IFNA(B1,0)` | 6 | `6` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| INDIRECT-073 | `indirect.json` | `=INDIRECT("R0C2",FALSE)` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): a row or column number 0 in R1C1 text is #VALUE! until a run observ… |
| INDIRECT-074 | `indirect.json` | `=INDIRECT("R1048577C2",FALSE)` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12, decided with the user: in R1C1 style, a row or column number past th… |
| INDIRECT-082 | `indirect.json` | `=INDIRECT("R[-5]C[-1]",FALSE)` | 9 | `9` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): an offset past the Sheet's edge is #VALUE! until a run tells where … |
| INDIRECT-083 | `indirect.json` | `=INDIRECT("R2C[-5]",FALSE)` | 9 | `9` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): an offset past the Sheet's edge is #VALUE! until a run tells where … |
| INDIRECT-084 | `indirect.json` | `=INDIRECT("R[1048575]C[-1]",FALSE)` | 7 | `7` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): an offset past the Sheet's edge is #VALUE! until a run tells where … |
| INDIRECT-090 | `indirect.json` | `=SUM(INDIRECT("R1C1:R2C1#",FALSE))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): a # after an R1C1 range, row or column is #VALUE! until a run obser… |
| INDIRECT-091 | `indirect.json` | `=SUM(INDIRECT("C1#",FALSE))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): a # after an R1C1 range, row or column is #VALUE! until a run obser… |
| INDIRECT-092 | `indirect.json` | `=SUM(INDIRECT("A1:A2#"))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): a # after an A1 range, row or column is #VALUE! until a run observe… |
| INDIRECT-094 | `indirect.json` | `=SUM(INDIRECT("R2C2:R3#",FALSE))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): a # after an R1C1 range, row or column, whatever the parts joined, … |
| INDIRECT-095 | `indirect.json` | `=INDIRECT("R2C16385",FALSE)` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12, decided with the user: in R1C1 style, a row or column number past th… |
| INDIRECT-096 | `indirect.json` | `=SUM(INDIRECT("A1:A2#"))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): a # after an A1 range, row or column is #VALUE! until a run observe… |
| INDIRECT-097 | `indirect.json` | `=SUM(INDIRECT("R1C1:R2C1#",FALSE))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): a # after an R1C1 range, row or column is #VALUE! until a run obser… |
| INDIRECT-098 | `indirect.json` | `=SUM(INDIRECT("A1#:A2"))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): a # inside an A1 join is #VALUE! until a run observes it, as in R1C… |
| INDIRECT-099 | `indirect.json` | `=SUM(INDIRECT("R1C1#:R2C1",FALSE))` | `#REF!` | `#REF!` | `{"value2": "#VALUE!"}` | ADR-0124 (2026-10-12): a # inside an R1C1 join is #VALUE! until a run observes it, as in A… |
| INDIRECT-100 | `indirect.json` | `=IFERROR(INDIRECT("B1"),0)` | 6 | `6` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| ISERROR-011 | `iserror.json` | `=ISERROR(B1)` | `False` | `FALSE` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| LET-113 | `let.json` | `=LET(x,B1,IFERROR(x,0))` | 6 | `6` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| TABLE-024 | `linked-tables.json` | `=COLUMNS(Positions)` | 2 | `2` | `{"value2": "#NAME?"}` | ADR-0049 (rule 2: a Linked Table is read by key, never by position, and a whole column by … |
| REF-015 | `references.json` | `=IF(TRUE,1,B1)` | 1 | `1` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |
| SEQUENCE-009 | `sequence.json` | `=IFERROR(SUM(B1#),0)` | 6 | `6` | `{"value2": "#VALUE!"}` | ADR-0047 (2026-10-12, decided with the user: a refusal is never caught, anywhere): MROUND'… |

## Agreed and promoted

| Case | File | Formula | Excel | Excel's text |
|---|---|---|---|---|
| AGGREGATE-068 | `aggregate.json` | `=AGGREGATE(15,6,A1:A11,9.5)` | `#NUM!` | `#NUM!` |
| EDATE-021 | `edate.json` | `=EDATE(0,3)` | 91 | `91` |
| EDATE-022 | `edate.json` | `=EDATE(0.5,12)` | 366 | `366` |
| FILTER-012 | `filter.json` | `=COUNTA(FILTER(Q1:Q2,H1:H2>0))` | 2 | `2` |
| FILTER-013 | `filter.json` | `=ISBLANK(INDEX(FILTER(Q1:Q2,H1:H2>0),2))` | `True` | `TRUE` |
| FV-023 | `fv.json` | `=FV(0.06/12,11.25,-100)` | 1154.2771752172287 | `$1,154.28 ` |
| FV-024 | `fv.json` | `=FV(0.42,-0.5,-100)` | -38.290152786502581 | `($38.29)` |
| INDIRECT-067 | `indirect.json` | `=INDIRECT("RC2",FALSE)` | 45 | `45` |
| INDIRECT-068 | `indirect.json` | `=INDIRECT("R2C",FALSE)` | 1.333 | `1.333` |
| INDIRECT-069 | `indirect.json` | `=SUM(INDIRECT("R[1]C[-1]:RC[-1]",FALSE))` | 46.332999999999998 | `46.333` |
| INDIRECT-070 | `indirect.json` | `=INDIRECT("R[1]C[0]",FALSE)` | 1.333 | `1.333` |
| INDIRECT-071 | `indirect.json` | `=INDIRECT("R[01]C[-1]",FALSE)` | 45 | `45` |
| INDIRECT-072 | `indirect.json` | `=INDIRECT("R3C02",FALSE)` | 45 | `45` |
| INDIRECT-077 | `indirect.json` | `=ROWS(INDIRECT("C2:R3",FALSE))` | `#REF!` | `#REF!` |
| INDIRECT-078 | `indirect.json` | `=SUM(INDIRECT("RC[-1]#",FALSE))` | 6 | `6` |
| INDIRECT-079 | `indirect.json` | `=SUM(INDIRECT("R1C1#",FALSE))` | `#REF!` | `#REF!` |
| INDIRECT-080 | `indirect.json` | `=INDIRECT("Positions[[PV]]",FALSE)` | 250.5 | `250.5` |
| INDIRECT-081 | `indirect.json` | `=INDIRECT("Missing[PV]",FALSE)` | `#REF!` | `#REF!` |
| INDIRECT-093 | `indirect.json` | `=INDIRECT("Sheet2!R3C2",FALSE)` | `#REF!` | `#REF!` |
| MOD-037 | `mod.json` | `=MOD(-0.0000000000000001,1)` | 0.99999999999999989 | `1` |
| MOD-038 | `mod.json` | `=MOD(1E-20,1)` | 9.9999999999999995E-21 | `1E-20` |
| MOD-040 | `mod.json` | `=MOD(-0.006664,5250)` | 5249.9933360000005 | `5249.993336` |
| PV-014 | `pv.json` | `=PV(0.42,0.5,-100)` | 38.290152786502574 | `$38.29 ` |
| PV-015 | `pv.json` | `=PV(0.06/12,-2.5,-100)` | -250.93828076244577 | `($250.94)` |
| PV-016 | `pv.json` | `=PV(0.06/12,11.25,-100)` | 1091.2943662943903 | `$1,091.29 ` |
| SMALL-011 | `small.json` | `=SMALL(A1:A9,9.2)` | `#NUM!` | `#NUM!` |
| SORT-027 | `sort.json` | ZZ1001, spilled by `=NA()` in A2, `=SORT(A1:A2)` in ZZ1000 | `#N/A` | `#N/A` |
| SORT-033 | `sort.json` | `=SUM(SORT(Q1:Q2))` | 7 | `7` |
| SORT-036 | `sort.json` | `=INDEX(SORT(Q1:Q2),2)` | 0 | `0` |
| SORT-040 | `sort.json` | ZZ1002, spilled by `=SORT(A1:A3,1,-1)` in ZZ1000 | 0 | `0` |
| SORT-041 | `sort.json` | AAA1001, spilled by `=SORT(A1:B3)` in ZZ1000 | `x` (text) | `x` |
| SORTBY-011 | `sortby.json` | `=COUNTA(SORTBY(Q1:Q2,H1:H2))` | 2 | `2` |
| SORTBY-012 | `sortby.json` | `=ISBLANK(INDEX(SORTBY(Q1:Q2,H1:H2,-1),1))` | `True` | `TRUE` |
| SORTBY-013 | `sortby.json` | `=SORTBY(Q1:Q2,H1:H2,-1)` | 0 | `0` |
| SORTBY-014 | `sortby.json` | `=SORTBY(H1:H2,Z1:Z2,-1)` | 20 | `20` |
| TRANSPOSE-005 | `transpose.json` | `=COUNTA(TRANSPOSE(Q1:Q2))` | 2 | `2` |
| TRANSPOSE-006 | `transpose.json` | `=ISBLANK(INDEX(TRANSPOSE(Q1:Q2),1,2))` | `True` | `TRUE` |
| UNIQUE-020 | `unique.json` | `=ISBLANK(INDEX(UNIQUE(Q1:Q2),2))` | `True` | `TRUE` |
| UNIQUE-024 | `unique.json` | ZZ1001, spilled by `=UNIQUE(A1:A3)&""` in ZZ1000 | `` (text) | `` |
| XNPV-020 | `xnpv.json` | `=XNPV(-0.01,A1:A5,B1:B5)` | `#NUM!` | `#NUM!` |

## Every case the pass asked, by file

Each case once, by its pass of record. A case promoted is one whose `source` this pass rewrote to `observed`.

| File | agreed | of which promoted | disagreed | blocked | recorded |
|---|---|---|---|---|---|
| `aggregate.json` | 68 | AGGREGATE-068 | – | – | AGGREGATE-069 |
| `count.json` | 8 | – | – | – | COUNT-009 |
| `counta.json` | 8 | – | – | – | COUNTA-009 |
| `datedif.json` | 0 | – | DATEDIF-048 | DATEDIF-001, DATEDIF-002, DATEDIF-003, DATEDIF-004, DATEDIF-005, DATEDIF-006, DATEDIF-007, DATEDIF-008, DATEDIF-009, DATEDIF-010, DATEDIF-011, DATEDIF-012, DATEDIF-013, DATEDIF-014, DATEDIF-015, DATEDIF-016, DATEDIF-017, DATEDIF-018, DATEDIF-019, DATEDIF-020, DATEDIF-021, DATEDIF-022, DATEDIF-023, DATEDIF-024, DATEDIF-025, DATEDIF-026, DATEDIF-027, DATEDIF-028, DATEDIF-029, DATEDIF-030, DATEDIF-031, DATEDIF-032, DATEDIF-033, DATEDIF-034, DATEDIF-035, DATEDIF-036, DATEDIF-037, DATEDIF-038, DATEDIF-039, DATEDIF-040, DATEDIF-041, DATEDIF-042, DATEDIF-043, DATEDIF-044, DATEDIF-045, DATEDIF-046, DATEDIF-047 | – |
| `edate.json` | 20 | EDATE-021, EDATE-022 | EDATE-018, EDATE-019, EDATE-020 | – | – |
| `eomonth.json` | 18 | – | – | – | – |
| `error-type.json` | 16 | – | – | ERRORTYPE-013, ERRORTYPE-016, ERRORTYPE-017, ERRORTYPE-018, ERRORTYPE-019 | ERRORTYPE-022 |
| `filter.json` | 13 | FILTER-012, FILTER-013 | FILTER-014 | – | – |
| `fv.json` | 24 | FV-023, FV-024 | – | – | – |
| `hour.json` | 4 | – | HOUR-005 | – | – |
| `iferror.json` | 16 | – | – | – | IFERROR-017, IFERROR-018, IFERROR-019, IFERROR-020 |
| `ifna.json` | 7 | – | – | – | IFNA-008 |
| `indirect.json` | 78 | INDIRECT-067, INDIRECT-068, INDIRECT-069, INDIRECT-070, INDIRECT-071, INDIRECT-072, INDIRECT-077, INDIRECT-078, INDIRECT-079, INDIRECT-080, INDIRECT-081, INDIRECT-093 | INDIRECT-075, INDIRECT-076 | – | INDIRECT-073, INDIRECT-074, INDIRECT-082, INDIRECT-083, INDIRECT-084, INDIRECT-090, INDIRECT-091, INDIRECT-092, INDIRECT-094, INDIRECT-095, INDIRECT-096, INDIRECT-097, INDIRECT-098, INDIRECT-099, INDIRECT-100 |
| `iserror.json` | 10 | – | – | – | ISERROR-011 |
| `let.json` | 0 | – | – | – | LET-113 |
| `linked-tables.json` | 17 | – | – | TABLE-005, TABLE-006, TABLE-007, TABLE-008, TABLE-009, TABLE-010 | TABLE-024 |
| `minute.json` | 2 | – | MINUTE-003 | – | – |
| `mod.json` | 35 | MOD-037, MOD-038, MOD-040 | MOD-033, MOD-034, MOD-035, MOD-036, MOD-039 | – | – |
| `networkdays.json` | 10 | – | – | – | – |
| `pmt.json` | 33 | – | PMT-034, PMT-035, PMT-036, PMT-037, PMT-038, PMT-039, PMT-040, PMT-041, PMT-042, PMT-043, PMT-044, PMT-045, PMT-046, PMT-047, PMT-048, PMT-049, PMT-050, PMT-051, PMT-052, PMT-053 | – | – |
| `pv.json` | 15 | PV-014, PV-015, PV-016 | PV-013 | – | – |
| `references.json` | 14 | – | – | – | REF-015 |
| `second.json` | 2 | – | SECOND-003 | – | – |
| `sequence.json` | 8 | – | – | – | SEQUENCE-009 |
| `small.json` | 11 | SMALL-011 | – | – | – |
| `sort.json` | 30 | SORT-027, SORT-033, SORT-036, SORT-040, SORT-041 | SORT-026, SORT-028, SORT-029, SORT-030, SORT-031, SORT-032, SORT-034, SORT-035, SORT-037, SORT-038, SORT-039, SORT-042 | – | – |
| `sortby.json` | 13 | SORTBY-011, SORTBY-012, SORTBY-013, SORTBY-014 | SORTBY-010 | – | – |
| `transpose.json` | 6 | TRANSPOSE-005, TRANSPOSE-006 | TRANSPOSE-007 | – | – |
| `unique.json` | 21 | UNIQUE-020, UNIQUE-024 | UNIQUE-021, UNIQUE-022, UNIQUE-023 | – | – |
| `workday.json` | 11 | – | – | – | – |
| `xnpv.json` | 18 | XNPV-020 | XNPV-018, XNPV-019, XNPV-021, XNPV-022, XNPV-023, XNPV-024, XNPV-025, XNPV-026, XNPV-027, XNPV-028, XNPV-029, XNPV-030 | – | – |
| `yearfrac.json` | 0 | – | YEARFRAC-058 | YEARFRAC-001, YEARFRAC-002, YEARFRAC-003, YEARFRAC-004, YEARFRAC-005, YEARFRAC-006, YEARFRAC-007, YEARFRAC-008, YEARFRAC-009, YEARFRAC-010, YEARFRAC-011, YEARFRAC-012, YEARFRAC-013, YEARFRAC-014, YEARFRAC-015, YEARFRAC-016, YEARFRAC-017, YEARFRAC-018, YEARFRAC-019, YEARFRAC-020, YEARFRAC-021, YEARFRAC-022, YEARFRAC-023, YEARFRAC-024, YEARFRAC-025, YEARFRAC-026, YEARFRAC-027, YEARFRAC-028, YEARFRAC-029, YEARFRAC-030, YEARFRAC-031, YEARFRAC-032, YEARFRAC-033, YEARFRAC-034, YEARFRAC-035, YEARFRAC-036, YEARFRAC-037, YEARFRAC-038, YEARFRAC-039, YEARFRAC-040, YEARFRAC-041, YEARFRAC-042, YEARFRAC-043, YEARFRAC-044, YEARFRAC-045, YEARFRAC-046, YEARFRAC-047, YEARFRAC-048, YEARFRAC-049, YEARFRAC-050, YEARFRAC-051, YEARFRAC-052, YEARFRAC-053, YEARFRAC-054, YEARFRAC-055, YEARFRAC-056, YEARFRAC-057 | – |

## The passes

| Results file | Regional format | How | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|
| `results-2026-10-14-run18a-com-en-US.json` | en-US | COM | 536 | 66 | 115 | 28 |
| `results-2026-10-14-run18a-keys-en-US.json` | en-US | keys | 0 | 0 | 0 | 1 |

Sources rewritten by `-Update`: uncertain → observed: 40.

Still not `observed` after the pass (105): AGGREGATE-069 (uncertain, recorded), COUNT-009 (uncertain, recorded), COUNTA-009 (uncertain, recorded), DATEDIF-048 (uncertain, disagree), EDATE-018 (uncertain, disagree), EDATE-019 (uncertain, disagree), EDATE-020 (uncertain, disagree), EDATE-023 (uncertain, agree), EOMONTH-018 (uncertain, agree), ERRORTYPE-022 (uncertain, recorded), FILTER-014 (uncertain, disagree), HOUR-005 (uncertain, disagree), IFERROR-017 (uncertain, recorded), IFERROR-018 (uncertain, recorded), IFERROR-019 (uncertain, recorded), IFERROR-020 (uncertain, recorded), IFNA-008 (uncertain, recorded), INDIRECT-073 (uncertain, recorded), INDIRECT-074 (uncertain, recorded), INDIRECT-075 (uncertain, disagree), INDIRECT-076 (uncertain, disagree), INDIRECT-082 (uncertain, recorded), INDIRECT-083 (uncertain, recorded), INDIRECT-084 (uncertain, recorded), INDIRECT-090 (uncertain, recorded), INDIRECT-091 (uncertain, recorded), INDIRECT-092 (uncertain, recorded), INDIRECT-094 (uncertain, recorded), INDIRECT-095 (uncertain, recorded), INDIRECT-096 (uncertain, recorded), INDIRECT-097 (uncertain, recorded), INDIRECT-098 (uncertain, recorded), INDIRECT-099 (uncertain, recorded), INDIRECT-100 (uncertain, recorded), ISERROR-011 (uncertain, recorded), LET-113 (uncertain, recorded), TABLE-005 (documented, blocked), TABLE-006 (documented, blocked), TABLE-007 (documented, blocked), TABLE-008 (documented, blocked), TABLE-009 (documented, blocked), TABLE-010 (documented, blocked), TABLE-024 (uncertain, recorded), MINUTE-003 (uncertain, disagree), MOD-033 (uncertain, disagree), MOD-034 (uncertain, disagree), MOD-035 (uncertain, disagree), MOD-036 (uncertain, disagree), MOD-039 (uncertain, disagree), NETWORKDAYS-010 (uncertain, agree), PMT-034 (uncertain, disagree), PMT-035 (uncertain, disagree), PMT-036 (uncertain, disagree), PMT-037 (uncertain, disagree), PMT-038 (uncertain, disagree), PMT-039 (uncertain, disagree), PMT-040 (uncertain, disagree), PMT-041 (uncertain, disagree), PMT-042 (uncertain, disagree), PMT-043 (uncertain, disagree), PMT-044 (uncertain, disagree), PMT-045 (uncertain, disagree), PMT-046 (uncertain, disagree), PMT-047 (uncertain, disagree), PMT-048 (uncertain, disagree), PMT-049 (uncertain, disagree), PMT-050 (uncertain, disagree), PMT-051 (uncertain, disagree), PMT-052 (uncertain, disagree), PMT-053 (uncertain, disagree), PV-013 (uncertain, disagree), REF-015 (uncertain, recorded), SECOND-003 (uncertain, disagree), SEQUENCE-009 (uncertain, recorded), SORT-026 (uncertain, disagree), SORT-028 (uncertain, disagree), SORT-029 (uncertain, disagree), SORT-030 (uncertain, disagree), SORT-031 (uncertain, disagree), SORT-032 (uncertain, disagree), SORT-034 (uncertain, disagree), SORT-035 (uncertain, disagree), SORT-037 (uncertain, disagree), SORT-038 (uncertain, disagree), SORT-039 (uncertain, disagree), SORT-042 (uncertain, disagree), SORTBY-010 (uncertain, disagree), TRANSPOSE-007 (uncertain, disagree), UNIQUE-021 (uncertain, disagree), UNIQUE-022 (uncertain, disagree), UNIQUE-023 (uncertain, disagree), WORKDAY-011 (uncertain, agree), XNPV-018 (uncertain, disagree), XNPV-019 (uncertain, disagree), XNPV-021 (uncertain, disagree), XNPV-022 (uncertain, disagree), XNPV-023 (uncertain, disagree), XNPV-024 (uncertain, disagree), XNPV-025 (uncertain, disagree), XNPV-026 (uncertain, disagree), XNPV-027 (uncertain, disagree), XNPV-028 (uncertain, disagree), XNPV-029 (uncertain, disagree), XNPV-030 (uncertain, disagree), YEARFRAC-058 (uncertain, disagree).
