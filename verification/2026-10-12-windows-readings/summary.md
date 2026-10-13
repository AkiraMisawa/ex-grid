## R1

| Case | Step | Keys | Mode | Bar (UI Automation) | Selected in the bar | Committed | Ctrl+C | Shift+Home, Ctrl+C | Cells after |
|---|---|---|---|---|---|---|---|---|---|
| press-at-box-edge | 1 | `press(3819,295)` | Ready | (empty) | (empty) |  |  |  |  |
| press-at-box-edge | 2 | `press(3813,284), the button again` | Ready | (empty) | (empty) |  |  |  |  |
| i | 1 | `press(3793,295)` | Edit | (empty) | (empty) | False | (empty) | (empty) |  |
| i | 2 | `press(3793,295) =` | Edit | `=` | (empty) | False | (empty) | `=` |  |
| i | 3 | `press(3793,295) = {DOWN}` | Edit | `=` | (empty) | False | (empty) | `=` |  |
| ii | 1 | `press(3793,295)` | Edit | (empty) | (empty) | False | (empty) | (empty) |  |
| ii | 2 | `press(3793,295) =` | Edit | `=` | (empty) | False | (empty) | `=` |  |
| ii | 3 | `press(3793,295) = {DOWN}` | Edit | `=` | (empty) | False | (empty) | `=` |  |
| ii | 4 | `press(3793,295) = {DOWN} {F4}` | Edit | `=` | (empty) | False | (empty) | `=` |  |
| ii | 5 | `press(3793,295) = {DOWN} {F4} {DOWN}` | Edit | `=` | (empty) | False | (empty) | `=` |  |
| iii | 1 | `press(3793,295)` | Edit | (empty) | (empty) | False | (empty) | (empty) |  |
| iii | 2 | `press(3793,295) =` | Edit | `=` | (empty) | False | (empty) | `=` |  |
| iii | 3 | `press(3793,295) = {DOWN}` | Edit | `=` | (empty) | False | (empty) | `=` |  |
| iii | 4 | `press(3793,295) = {DOWN} {F4}` | Edit | `=` | (empty) | False | (empty) | `=` |  |
| iii | 5 | `press(3793,295) = {DOWN} {F4} {+}` | Edit | `=+` | (empty) | False | (empty) | `=+` |  |
| iv | 1 | `press(3793,295)` | Edit | (empty) | (empty) | False | (empty) | (empty) |  |
| iv | 2 | `press(3793,295) =B2` | Edit | `=B2` | (empty) | False | (empty) | `=B2` |  |
| iv | 3 | `press(3793,295) =B2 {F4}` | Edit | `=$B$2` | (empty) | False | (empty) | `=$B$2` |  |
| v | 1 | `press(3793,295)` | Edit | (empty) | (empty) | False | (empty) | (empty) |  |
| v | 2 | `press(3793,295) =SUM{(}A1:B2{)}` | Edit | `=SUM(A1:B2)` | (empty) | False | (empty) | `=SUM(A1:B2)` |  |
| v | 3 | `press(3793,295) =SUM{(}A1:B2{)} {LEFT}` | Edit | `=SUM(A1:B2)` | (empty) | False | (empty) | `=SUM(A1:B2` |  |
| v | 4 | `press(3793,295) =SUM{(}A1:B2{)} {LEFT} {F4}` | Edit | `=SUM(A1:$B$2)` | `$B$2` | False | `$B$2` | `=SUM(A1:` |  |
| vi | 1 | `=` | Enter | `=` | (empty) | False | (empty) | `A10:D10` |  |
| vi | 2 | `= {DOWN}` | Point | `=D11` | `D11` | False | `D11` | `A10:D10` |  |
| vi | 3 | `= {DOWN} +{DOWN}` | Point | `=D11:D12` | `D11:D12` | False | `D11:D12` | `A10:D10` |  |
| vi | 4 | `= {DOWN} +{DOWN} {F4}` | Point | `=$D$11:$D$12` | `$D$11:$D$12` | False | `$D$11:$D$12` | `A10:D10` |  |
| vii | 1 | `abc +{RIGHT}` | Ready | `abc` | (empty) | True |  |  | D10 String "abc" — active=Sheet1!D10 selection=D10:E10 |
| vii | 2 | `abc +{DOWN}` | Ready | `abc` | (empty) | True |  |  | D10 String "abc" — active=Sheet1!D10 selection=D10:D11 |
| vii | 3 | `=A1{+}B2 +{LEFT}` | Ready | `=A1+B2` | (empty) | True |  |  | D10 number 0 — active=Sheet1!D10 selection=C10:D10 |
| vii | 4 | `abc +{END}` | Ready | `abc` | (empty) | True |  |  | D10 String "abc" — active=Sheet1!D10 selection=D10 |
| vii | 5 | `abc ^{RIGHT}` | Ready | (empty) | (empty) | True |  |  | D10 String "abc" — active=Sheet1!XFD10 selection=XFD10 |
| vii | 6 | `abc ^+{RIGHT}` | Ready | `abc` | (empty) | True |  |  | D10 String "abc" — active=Sheet1!D10 selection=D10:XFD10 |
| viii | 1 | `=` | Enter | `=` | (empty) | False | (empty) | `A10:D10` |  |
| viii | 2 | `= {DOWN}` | Point | `=D11` | `D11` | False | `D11` | `A10:D10` |  |
| viii | 3 | `= {DOWN} +{HOME}` | Point | `=A11:D11` | `A11:D11` | False | `A11:D11` | `A10:D10` |  |

## R4

| Zoom | Case | Active | Selection | Left edge | Right edge | Top edge | Bottom edge |
|---|---|---|---|---|---|---|---|
| 100% | 1 | D4 | A1:B2,D4:E6 | #FFFFFF x4 #AFAFAF x1 #FFFFFF x1 #217346 x1 #FFFFFF x10 | #FFFFFF x6 #217346 x1 #FFFFFF x1 #AFAFAF x1 #C7C7C7 x8 | #FFFFFF x4 #AFAFAF x1 #FFFFFF x1 #217346 x1 #FFFFFF x10 | #FFFFFF x6 #217346 x1 #FFFFFF x1 #AFAFAF x1 #C7C7C7 x8 |
| 100% | 2 | A1 | A1:B2,D4:E6 | #E1E1E1 x3 #217346 x2 #FFFFFF x1 #217346 x1 #FFFFFF x10 | #FFFFFF x6 #217346 x1 #FFFFFF x1 #AFAFAF x1 #C7C7C7 x8 | #E1E1E1 x4 #217346 x2 #FFFFFF x1 #217346 x1 #FFFFFF x9 | #FFFFFF x8 #217346 x1 #FFFFFF x1 #AFAFAF x1 #C7C7C7 x6 |
| 100% | 3 | D4 | A1:B2,D4 | #FFFFFF x4 #AFAFAF x1 #FFFFFF x1 #217346 x1 #FFFFFF x10 | #FFFFFF x6 #217346 x1 #FFFFFF x1 #AFAFAF x1 #FFFFFF x8 | #FFFFFF x4 #AFAFAF x1 #FFFFFF x1 #217346 x1 #FFFFFF x10 | #FFFFFF x6 #217346 x1 #FFFFFF x1 #AFAFAF x1 #FFFFFF x8 |
| 150% | 1 | D4 | A1:B2,D4:E6 | #FFFFFF x5 #AFAFAF x1 #FFFFFF x1 #217346 x1 #FFFFFF x9 | #FFFFFF x7 #217346 x1 #FFFFFF x1 #AFAFAF x1 #C7C7C7 x7 | #FFFFFF x3 #AFAFAF x1 #FFFFFF x1 #217346 x1 #FFFFFF x11 | #FFFFFF x7 #217346 x1 #FFFFFF x1 #AFAFAF x1 #C7C7C7 x7 |
| 150% | 2 | A1 | A1:B2,D4:E6 | #E1E1E1 x4 #217346 x2 #FFFFFF x1 #217346 x1 #FFFFFF x9 | #FFFFFF x7 #217346 x1 #FFFFFF x1 #AFAFAF x1 #C7C7C7 x7 | #E1E1E1 x3 #217346 x2 #FFFFFF x1 #217346 x1 #FFFFFF x10 | #FFFFFF x8 #217346 x1 #FFFFFF x1 #AFAFAF x1 #C7C7C7 x6 |
| 150% | 3 | D4 | A1:B2,D4 | #FFFFFF x5 #AFAFAF x1 #FFFFFF x1 #217346 x1 #FFFFFF x9 | #FFFFFF x7 #217346 x1 #FFFFFF x1 #AFAFAF x1 #FFFFFF x7 | #FFFFFF x3 #AFAFAF x1 #FFFFFF x1 #217346 x1 #FFFFFF x11 | #FFFFFF x7 #217346 x1 #FFFFFF x1 #AFAFAF x1 #FFFFFF x7 |

## R6

| Case | Step | Bold | Italic | Underline | A1 shows | Column A width | Standard width used |
|---|---|---|---|---|---|---|---|
| font-italic | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| font-italic | 2-after-ok | False | True | -4142 | `123456789.12` | 12.45 | False |
| font-underline | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| font-underline | 2-after-ok | False | False | 2 | `123456789.12` | 11.73 | False |
| font-colour | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| font-colour | 2-after-ok | False | False | -4142 | `123456789.12` | 11.73 | False |
| fill-tab-only | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| fill-tab-only | 2-after-ok | False | False | -4142 | `########` | 8.09 | True |
| fill-yellow | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| fill-yellow | 2-after-ok | False | False | -4142 | `########` | 8.09 | True |
| alignment-tab-only | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| alignment-tab-only | 2-after-ok | False | False | -4142 | `########` | 8.09 | True |
| alignment-vertical-top | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| alignment-vertical-top | 2-after-ok | False | False | -4142 | `########` | 8.09 | True |
| button-bold | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| button-bold | 1-bold-pressed | True | False | -4142 | `########` | 8.09 | True |
| button-italic | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| button-italic | 1-italic-pressed | False | True | -4142 | `#######` | 8.09 | True |
| button-underline | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| button-underline | 1-underline-pressed | False | False | 2 | `123456789.12` | 11.73 | False |
| ctrl-b-twice | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| ctrl-b-twice | 1-ctrl-b | True | False | -4142 | `########` | 8.09 | True |
| ctrl-b-twice | 2-ctrl-b-again | False | False | -4142 | `########` | 8.09 | True |
| com-bold | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| com-bold | 1-font-bold-True | True | False | -4142 | `########` | 8.09 | True |
| com-underline | 0-hashes | False | False | -4142 | `########` | 8.09 | True |
| com-underline | 1-font-underline-2 | False | False | 2 | `123456789.12` | 11.73 | False |

## R8

| Case | Selection | Edges set (cell, edge, LineStyle, Weight) | What each cell holds after Escape |
|---|---|---|---|
| 1-thin-top-thick-right | B2 | B2/8/1/2, B2/10/1/4 | B2: top=1/2 bottom=-4142/2 left=-4142/2 right=1/4 |
| 2-range-different-lines | A2:B3 | A2/8/1/2, B2/10/1/-4138, A3/9/1/4, B3/7/-4115/2 | A2: top=1/2 bottom=-4142/2 left=-4142/2 right=-4142/2 ;; A3: top=-4142/2 bottom=1/4 left=-4142/2 right=-4115/2 ;; B2: top=-4142/2 bottom=-4142/2 left=-4142/2 right=1/-4138 ;; B3: top=-4142/2 bottom=-4142/2 left=-4115/2 right=-4142/2 |
| 3-no-lines | D5 | none | D5: top=-4142/2 bottom=-4142/2 left=-4142/2 right=-4142/2 |

## R3 (a) and (c): the cells

**e05c, Excel**

| `Row Labels` | `Average of Note` | `StdDev of Note` | `Var of Note` | `StdDevp of Note` | `Varp of Note` | `Sum of Note` | `Max. of Note` | `Min. of Note` | `Product of Note` | `Count of Note` | `Count of Note2` |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `A` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `0` | `0` | `0` | `0` | `2` | `0` |
| `B` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `0` | `0` | `0` | `0` | `1` | `0` |
| `C` |   |   |   |   |   |   |   |   |   |   |   |
| `Grand Total` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `0` | `0` | `0` | `0` | `3` | `0` |

**e05c, ExPivot**

| `Row Labels` | `Average of Note` | `StdDev of Note` | `Var of Note` | `StdDevp of Note` | `Varp of Note` | `Sum of Note` | `Max of Note` | `Min of Note` | `Product of Note` | `Count of Note` | `Count Numbers of Note` |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `A` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `0` | `0` | `0` | `0` | `2` | `0` |
| `B` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `0` | `0` | `0` | `0` | `1` | `0` |
| `C` |   |   |   |   |   |   |   |   |   |   |   |
| `Grand Total` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `#DIV/0!` | `0` | `0` | `0` | `0` | `3` | `0` |

Body cells the same, row by row: [True, True, True, True]; the headers that differ: [('Max. of Note', 'Max of Note'), ('Min. of Note', 'Min of Note'), ('Count of Note2', 'Count Numbers of Note')]

**e16b, Excel**

| `Row Labels` | `Sum of Amount` |
|---|---|
| `1.23456789` | `1` |
| `1.23457E+11` | `2` |
| `Grand Total` | `3` |

**e16b, ExPivot**

| `Row Labels` | `Sum of Amount` |
|---|---|
| `1.23456789012345` | `1` |
| `123456789012.345` | `2` |
| `Grand Total` | `3` |

Body cells the same, row by row: [False, False, True]; the headers that differ: []

