### e02: item 2 — gates 0.1.0

Text Items ignore case, and the first spelling in the records labels the Item.

The records (`part-e/e02.csv`): `Region,Amount` / `East,10` / `east,20` / `EAST,30` / `West,5`

Layout: rows ['Region'], columns [], values [('Amount', 'Sum', None)]

**Excel** (`A3:B6`; Value Fields [('Sum of Amount', 'General')]):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `East` | `60` |
| `West` | `5` |
| `Grand Total` | `65` |

**ExPivot** (the Areas: {'filters': [], 'columns': [], 'rows': ['Region'], 'values': ['Sum of Amount']}):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `East` | `60` |
| `West` | `5` |
| `Grand Total` | `65` |

**The body, cell by cell, by row label:** the same, every cell.

### e03: item 3 — gates 0.1.0

A missing value is (blank), is an Item like any other, and is counted by Count.

The records (`part-e/e03.csv`): `Region,Amount` / `East,10` / `,20` / `West,5` / `,1`

Layout: rows ['Region'], columns [], values [('Region', 'Count', None), ('Amount', 'Count', None), ('Amount', 'Sum', None)]

**Excel** (`A3:D7`; Value Fields [('Count of Region', 'General'), ('Count of Amount', 'General'), ('Sum of Amount', 'General')]):

| `Row Labels` | `Count of Region` | `Count of Amount` | `Sum of Amount` |
|---|---|---|---|
| `East` | `1` | `1` | `10` |
| `West` | `1` | `1` | `5` |
| `(blank)` |   | `2` | `21` |
| `Grand Total` | `2` | `4` | `36` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Σ Values'], 'rows': ['Region'], 'values': ['Count of Region', 'Count of Amount', 'Sum of Amount']}):

| `Row Labels` | `Count of Region` | `Count of Amount` | `Sum of Amount` |
|---|---|---|---|
| `East` | `1` | `1` | `10` |
| `West` | `1` | `1` | `5` |
| `(blank)` |   | `2` | `21` |
| `Grand Total` | `2` | `4` | `36` |

**The body, cell by cell, by row label:** the same, every cell.

### e04: item 4 — gates 0.1.0

Count counts every non-empty value, text included; Count Numbers counts numbers only.

The records (`part-e/e04.csv`): `Group,Value` / `A,10` / `A,x` / `A,` / `B,20` / `B,30`

Layout: rows ['Group'], columns [], values [('Value', 'Count', None), ('Value', 'Count Numbers', None)]

**Excel** (`A3:C6`; Value Fields [('Count of Value', 'General'), ('Count of Value2', 'General')]):

| `Row Labels` | `Count of Value` | `Count of Value2` |
|---|---|---|
| `A` | `2` | `1` |
| `B` | `2` | `2` |
| `Grand Total` | `4` | `3` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Σ Values'], 'rows': ['Group'], 'values': ['Count of Value', 'Count Numbers of Value']}):

| `Row Labels` | `Count of Value` | `Count Numbers of Value` |
|---|---|---|
| `A` | `2` | `0` |
| `B` | `2` | `0` |
| `Grand Total` | `4` | `0` |

**The body, cell by cell, by row label:** 
- **differs:** `A`, value column 2: Excel `1`, at an ample width `1` (Value2 1), ExPivot `0`
- **differs:** `B`, value column 2: Excel `2`, at an ample width `2` (Value2 2), ExPivot `0`
- **differs:** `Grand Total`, value column 2: Excel `3`, at an ample width `3` (Value2 3), ExPivot `0`

### e05a: item 5 (a) — gates 0.1.0

Average, StdDev and Var over no numbers are #DIV/0!.

The records (`part-e/e05a.csv`): `Group,Amount` / `A,` / `A,` / `C,1` / `C,3`

Layout: rows ['Group'], columns [], values [('Amount', 'Average', None), ('Amount', 'StdDev', None), ('Amount', 'Var', None)]

**Excel** (`A3:D6`; Value Fields [('Average of Amount', 'General'), ('StdDev of Amount', 'General'), ('Var of Amount', 'General')]):

| `Row Labels` | `Average of Amount` | `StdDev of Amount` | `Var of Amount` |
|---|---|---|---|
| `A` |   |   |   |
| `C` | `2` | `1.414213562` | `2` |
| `Grand Total` | `2` | `1.414213562` | `2` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Σ Values'], 'rows': ['Group'], 'values': ['Average of Amount', 'StdDev of Amount', 'Var of Amount']}):

| `Row Labels` | `Average of Amount` | `StdDev of Amount` | `Var of Amount` |
|---|---|---|---|
| `A` |   |   |   |
| `C` | `2` | `1.4142135623731` | `2` |
| `Grand Total` | `2` | `1.4142135623731` | `2` |

**The body, cell by cell, by row label:** 
- the same value, shown differently: `C`, value column 2: Excel shows `1.414213562`, at an ample width `1.414213562` (Value2 1.4142135623730951), ExPivot `1.4142135623731`
- the same value, shown differently: `Grand Total`, value column 2: Excel shows `1.414213562`, at an ample width `1.414213562` (Value2 1.4142135623730951), ExPivot `1.4142135623731`

### e05b: item 5 (b) — gates 0.1.0

StdDev and Var over one number are #DIV/0!; StdDevp and Varp over one number are 0.

The records (`part-e/e05b.csv`): `Group,Amount` / `B,5` / `C,1` / `C,3`

Layout: rows ['Group'], columns [], values [('Amount', 'StdDev', None), ('Amount', 'Var', None), ('Amount', 'StdDevp', None), ('Amount', 'Varp', None)]

**Excel** (`A3:E6`; Value Fields [('StdDev of Amount', 'General'), ('Var of Amount', 'General'), ('StdDevp of Amount', 'General'), ('Varp of Amount', 'General')]):

| `Row Labels` | `StdDev of Amount` | `Var of Amount` | `StdDevp of Amount` | `Varp of Amount` |
|---|---|---|---|---|
| `B` | `#DIV/0!` | `#DIV/0!` | `0` | `0` |
| `C` | `1.414213562` | `2` | `1` | `1` |
| `Grand Total` | `2` | `4` | `1.632993162` | `2.666666667` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Σ Values'], 'rows': ['Group'], 'values': ['StdDev of Amount', 'Var of Amount', 'StdDevp of Amount', 'Varp of Amount']}):

| `Row Labels` | `StdDev of Amount` | `Var of Amount` | `StdDevp of Amount` | `Varp of Amount` |
|---|---|---|---|---|
| `B` | `#DIV/0!` | `#DIV/0!` | `0` | `0` |
| `C` | `1.4142135623731` | `2` | `1` | `1` |
| `Grand Total` | `2` | `4` | `1.63299316185545` | `2.66666666666667` |

**The body, cell by cell, by row label:** 
- the same value, shown differently: `C`, value column 1: Excel shows `1.414213562`, at an ample width `1.414213562` (Value2 1.4142135623730951), ExPivot `1.4142135623731`
- the same value, shown differently: `Grand Total`, value column 3: Excel shows `1.632993162`, at an ample width `1.632993162` (Value2 1.6329931618554521), ExPivot `1.63299316185545`
- the same value, shown differently: `Grand Total`, value column 4: Excel shows `2.666666667`, at an ample width `2.666666667` (Value2 2.6666666666666665), ExPivot `2.66666666666667`

### e06a: item 6 (a) — gates 0.1.0

Product where there are no values: ExPivot shows an empty cell.

The records (`part-e/e06a.csv`): `Group,Amount` / `A,` / `A,` / `C,2` / `C,3`

Layout: rows ['Group'], columns [], values [('Amount', 'Product', None)]

**Excel** (`A3:B6`; Value Fields [('Product of Amount', 'General')]):

| `Row Labels` | `Product of Amount` |
|---|---|
| `A` |   |
| `C` | `6` |
| `Grand Total` | `6` |

**ExPivot** (the Areas: {'filters': [], 'columns': [], 'rows': ['Group'], 'values': ['Product of Amount']}):

| `Row Labels` | `Product of Amount` |
|---|---|
| `A` |   |
| `C` | `6` |
| `Grand Total` | `6` |

**The body, cell by cell, by row label:** the same, every cell.

### e06b: item 6 (b) — gates 0.1.0

Product over values none of which is a number shows 0.

The records (`part-e/e06b.csv`): `Group,Note` / `B,x` / `B,y` / `C,z`

Layout: rows ['Group'], columns [], values [('Note', 'Product', None)]

**Excel** (`A3:B6`; Value Fields [('Product of Note', 'General')]):

| `Row Labels` | `Product of Note` |
|---|---|
| `B` | `0` |
| `C` | `0` |
| `Grand Total` | `0` |

**ExPivot** (the Areas: {'filters': [], 'columns': [], 'rows': ['Group'], 'values': ['Product of Note']}):

| `Row Labels` | `Product of Note` |
|---|---|
| `B` | `0` |
| `C` | `0` |
| `Grand Total` | `0` |

**The body, cell by cell, by row label:** the same, every cell.

### e07: item 7 — gates 0.1.0

An empty cell stays empty, never 0.

The records (`part-e/e07.csv`): `Region,Product,Amount` / `East,Apple,10` / `West,Pear,5`

Layout: rows ['Region'], columns ['Product'], values [('Amount', 'Sum', None)]

**Excel** (`A3:D7`; Value Fields [('Sum of Amount', 'General')]):

| `Sum of Amount` | `Column Labels` |   |   |
|---|---|---|---|
| `Row Labels` | `Apple` | `Pear` | `Grand Total` |
| `East` | `10` |   | `10` |
| `West` |   | `5` | `5` |
| `Grand Total` | `10` | `5` | `15` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Product'], 'rows': ['Region'], 'values': ['Sum of Amount']}):

| `Row Labels` | `Apple` | `Pear` | `Grand Total` |
|---|---|---|---|
| `East` | `10` |   | `10` |
| `West` |   | `5` | `5` |
| `Grand Total` | `10` | `5` | `15` |

**The body, cell by cell, by row label:** the same, every cell.

### e08: item 8 — gates 0.1.0

A subtotal and a grand total are aggregated from the records: an Average's total is the average of every record, and a Count's grand total counts records.

The records (`part-e/e08.csv`): `Region,Desk,Amount` / `East,D1,1` / `East,D1,2` / `East,D1,3` / `East,D2,10` / `West,D3,100`

Layout: rows ['Region', 'Desk'], columns [], values [('Amount', 'Average', None), ('Amount', 'Count', None)]

**Excel** (`A3:C9`; Value Fields [('Average of Amount', 'General'), ('Count of Amount', 'General')]):

| `Row Labels` | `Average of Amount` | `Count of Amount` |
|---|---|---|
| `East` | `4` | `4` |
| `D1` | `2` | `3` |
| `D2` | `10` | `1` |
| `West` | `100` | `1` |
| `D3` | `100` | `1` |
| `Grand Total` | `23.2` | `5` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Σ Values'], 'rows': ['Region', 'Desk'], 'values': ['Average of Amount', 'Count of Amount']}):

| `Row Labels` | `Average of Amount` | `Count of Amount` |
|---|---|---|
| `−East` | `4` | `4` |
| `D1` | `2` | `3` |
| `D2` | `10` | `1` |
| `−West` | `100` | `1` |
| `D3` | `100` | `1` |
| `Grand Total` | `23.2` | `5` |

**The body, cell by cell, by row label:** the same, every cell.

### e15a: item 15 — gates 0.1.0

Show Values As % of Grand Total, in 0.00%.

The records (`part-e/e15a.csv`): `Region,Product,Amount` / `East,Apple,10` / `East,Pear,30` / `West,Apple,20` / `West,Pear,40`

Layout: rows ['Region'], columns ['Product'], values [('Amount', 'Sum', '% of Grand Total')]

**Excel** (`A3:D7`; Value Fields [('Sum of Amount', '0.00%')]):

| `Sum of Amount` | `Column Labels` |   |   |
|---|---|---|---|
| `Row Labels` | `Apple` | `Pear` | `Grand Total` |
| `East` | `10.00%` | `30.00%` | `40.00%` |
| `West` | `20.00%` | `40.00%` | `60.00%` |
| `Grand Total` | `30.00%` | `70.00%` | `100.00%` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Product'], 'rows': ['Region'], 'values': ['Sum of Amount']}):

| `Row Labels` | `Apple` | `Pear` | `Grand Total` |
|---|---|---|---|
| `East` | `10.00%` | `30.00%` | `40.00%` |
| `West` | `20.00%` | `40.00%` | `60.00%` |
| `Grand Total` | `30.00%` | `70.00%` | `100.00%` |

**The body, cell by cell, by row label:** the same, every cell.

### e15b: item 15 — gates 0.1.0

Show Values As % of Column Total, a total's share of itself 100.00%.

The records (`part-e/e15b.csv`): `Region,Product,Amount` / `East,Apple,10` / `East,Pear,30` / `West,Apple,20` / `West,Pear,40`

Layout: rows ['Region'], columns ['Product'], values [('Amount', 'Sum', '% of Column Total')]

**Excel** (`A3:D7`; Value Fields [('Sum of Amount', '0.00%')]):

| `Sum of Amount` | `Column Labels` |   |   |
|---|---|---|---|
| `Row Labels` | `Apple` | `Pear` | `Grand Total` |
| `East` | `33.33%` | `42.86%` | `40.00%` |
| `West` | `66.67%` | `57.14%` | `60.00%` |
| `Grand Total` | `100.00%` | `100.00%` | `100.00%` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Product'], 'rows': ['Region'], 'values': ['Sum of Amount']}):

| `Row Labels` | `Apple` | `Pear` | `Grand Total` |
|---|---|---|---|
| `East` | `33.33%` | `42.86%` | `40.00%` |
| `West` | `66.67%` | `57.14%` | `60.00%` |
| `Grand Total` | `100.00%` | `100.00%` | `100.00%` |

**The body, cell by cell, by row label:** the same, every cell.

### e15c: item 15 — gates 0.1.0

Show Values As % of Row Total.

The records (`part-e/e15c.csv`): `Region,Product,Amount` / `East,Apple,10` / `East,Pear,30` / `West,Apple,20` / `West,Pear,40`

Layout: rows ['Region'], columns ['Product'], values [('Amount', 'Sum', '% of Row Total')]

**Excel** (`A3:D7`; Value Fields [('Sum of Amount', '0.00%')]):

| `Sum of Amount` | `Column Labels` |   |   |
|---|---|---|---|
| `Row Labels` | `Apple` | `Pear` | `Grand Total` |
| `East` | `25.00%` | `75.00%` | `100.00%` |
| `West` | `33.33%` | `66.67%` | `100.00%` |
| `Grand Total` | `30.00%` | `70.00%` | `100.00%` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Product'], 'rows': ['Region'], 'values': ['Sum of Amount']}):

| `Row Labels` | `Apple` | `Pear` | `Grand Total` |
|---|---|---|---|
| `East` | `25.00%` | `75.00%` | `100.00%` |
| `West` | `33.33%` | `66.67%` | `100.00%` |
| `Grand Total` | `30.00%` | `70.00%` | `100.00%` |

**The body, cell by cell, by row label:** the same, every cell.

### e16: item 16 — gates 0.1.0

General writes at most 15 significant digits, as a cell does.

The records (`part-e/e16.csv`): `Group,Amount` / `A,0.1` / `A,0.2` / `B,123456789012.345678` / `C,1.23456789012345678` / `D,0.000012345678901234567`

Layout: rows ['Group'], columns [], values [('Amount', 'Sum', None)]

**Excel** (`A3:B8`; Value Fields [('Sum of Amount', 'General')]):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `A` | `0.3` |
| `B` | `1.23457E+11` |
| `C` | `1.23456789` |
| `D` | `1.23457E-05` |
| `Grand Total` | `1.23457E+11` |

**ExPivot** (the Areas: {'filters': [], 'columns': [], 'rows': ['Group'], 'values': ['Sum of Amount']}):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `A` | `0.3` |
| `B` | `123456789012.346` |
| `C` | `1.23456789012346` |
| `D` | `1.23456789012346E-05` |
| `Grand Total` | `123456789013.88` |

**The body, cell by cell, by row label:** 
- **differs:** `B`, value column 1: Excel `1.23457E+11`, at an ample width `1.23457E+11` (Value2 123456789012.345), ExPivot `123456789012.346`
- **differs:** `C`, value column 1: Excel `1.23456789`, at an ample width `1.23456789` (Value2 1.23456789012345), ExPivot `1.23456789012346`
- **differs:** `D`, value column 1: Excel `1.23457E-05`, at an ample width `1.23457E-05` (Value2 1.23456789012345E-05), ExPivot `1.23456789012346E-05`
- the same value, shown differently: `Grand Total`, value column 1: Excel shows `1.23457E+11`, at an ample width `1.23457E+11` (Value2 123456789013.87959), ExPivot `123456789013.88`

### e17: item 17 — gates 0.1.0

Hidden Items are left out of the totals.

The records (`part-e/e17.csv`): `Region,Amount` / `East,10` / `West,20` / `North,30`

Layout: rows ['Region'], columns [], values [('Amount', 'Sum', None)], hidden {'Region': ['North']}

**Excel** (`A3:B6`; Value Fields [('Sum of Amount', 'General')]):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `East` | `10` |
| `West` | `20` |
| `Grand Total` | `30` |

**ExPivot** (the Areas: {'filters': [], 'columns': [], 'rows': ['Region'], 'values': ['Sum of Amount']}):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `East` | `10` |
| `West` | `20` |
| `Grand Total` | `30` |

**The body, cell by cell, by row label:** the same, every cell.

### e01: item 1

Item order: numbers, dates, text (ignoring case), FALSE and TRUE, errors; (blank) last. ExPivot keeps one type per field: the Item order is read per kind (text here).

The records (`part-e/e01.csv`): `Key,Amount` / `banana,1` / `Apple,2` / `cherry,3` / `Apple,4` / `,5` / `Banana,6`

Layout: rows ['Key'], columns [], values [('Amount', 'Sum', None)]

**Excel** (`A3:B8`; Value Fields [('Sum of Amount', 'General')]):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `Apple` | `6` |
| `banana` | `7` |
| `cherry` | `3` |
| `(blank)` | `5` |
| `Grand Total` | `21` |

**ExPivot** (the Areas: {'filters': [], 'columns': [], 'rows': ['Key'], 'values': ['Sum of Amount']}):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `Apple` | `6` |
| `banana` | `7` |
| `cherry` | `3` |
| `(blank)` | `5` |
| `Grand Total` | `21` |

**The body, cell by cell, by row label:** the same, every cell.

### e09a: item 9

Compact form, subtotals at the top.

The records (`part-e/e09a.csv`): `Region,Desk,Amount` / `East,D1,1` / `East,D2,2` / `West,D3,4`

Layout: rows ['Region', 'Desk'], columns [], values [('Amount', 'Sum', None)], form compact

**Excel** (`A3:B9`; Value Fields [('Sum of Amount', 'General')]):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `East` | `3` |
| `D1` | `1` |
| `D2` | `2` |
| `West` | `4` |
| `D3` | `4` |
| `Grand Total` | `7` |

**ExPivot** (the Areas: {'filters': [], 'columns': [], 'rows': ['Region', 'Desk'], 'values': ['Sum of Amount']}):

| `Row Labels` | `Sum of Amount` |
|---|---|
| `−East` | `3` |
| `D1` | `1` |
| `D2` | `2` |
| `−West` | `4` |
| `D3` | `4` |
| `Grand Total` | `7` |

**The body, cell by cell, by row label:** the same, every cell.

### e09b: item 9

Outline form.

The records (`part-e/e09b.csv`): `Region,Desk,Amount` / `East,D1,1` / `East,D2,2` / `West,D3,4`

Layout: rows ['Region', 'Desk'], columns [], values [('Amount', 'Sum', None)], form outline

**Excel** (`A3:C9`; Value Fields [('Sum of Amount', 'General')]):

| `Region` | `Desk` | `Sum of Amount` |
|---|---|---|
| `East` |   | `3` |
|   | `D1` | `1` |
|   | `D2` | `2` |
| `West` |   | `4` |
|   | `D3` | `4` |
| `Grand Total` |   | `7` |

**ExPivot** (the Areas: {'filters': [], 'columns': [], 'rows': ['Region', 'Desk'], 'values': ['Sum of Amount']}):

| `Region` | `Desk` | `Sum of Amount` |
|---|---|---|
| `−East` |   | `3` |
|   | `D1` | `1` |
|   | `D2` | `2` |
| `−West` |   | `4` |
|   | `D3` | `4` |
| `Grand Total` |   | `7` |

**The body, cell by cell, by row label:** the same, every cell.

### e09c: item 9

Tabular form, the subtotal at the bottom, captioned East Total.

The records (`part-e/e09c.csv`): `Region,Desk,Amount` / `East,D1,1` / `East,D2,2` / `West,D3,4`

Layout: rows ['Region', 'Desk'], columns [], values [('Amount', 'Sum', None)], form tabular

**Excel** (`A3:C9`; Value Fields [('Sum of Amount', 'General')]):

| `Region` | `Desk` | `Sum of Amount` |
|---|---|---|
| `East` | `D1` | `1` |
|   | `D2` | `2` |
| `East Total` |   | `3` |
| `West` | `D3` | `4` |
| `West Total` |   | `4` |
| `Grand Total` |   | `7` |

**ExPivot** (the Areas: {'filters': [], 'columns': [], 'rows': ['Region', 'Desk'], 'values': ['Sum of Amount']}):

| `Region` | `Desk` | `Sum of Amount` |
|---|---|---|
| `−East` | `D1` | `1` |
|   | `D2` | `2` |
| `East Total` |   | `3` |
| `−West` | `D3` | `4` |
| `West Total` |   | `4` |
| `Grand Total` |   | `7` |

**The body, cell by cell, by row label:** the same, every cell.

### e12: item 12

Column Items as header bands, a column field's total captioned 'FALSE Total', Grand Total on the right.

The records (`part-e/e12.csv`): `Region,Flag,Side,Amount` / `East,TRUE,Buy,1` / `East,FALSE,Sell,2` / `West,FALSE,Buy,4` / `West,TRUE,Sell,8`

Layout: rows ['Region'], columns ['Flag', 'Side'], values [('Amount', 'Sum', None)]

**Excel** (`A3:H8`; Value Fields [('Sum of Amount', 'General')]):

| `Sum of Amount` | `Column Labels` |   |   |   |   |   |   |
|---|---|---|---|---|---|---|---|
|   | `FALSE` |   | `FALSE Total` | `TRUE` |   | `TRUE Total` | `Grand Total` |
| `Row Labels` | `Buy` | `Sell` |   | `Buy` | `Sell` |   |   |
| `East` |   | `2` | `2` | `1` |   | `1` | `3` |
| `West` | `4` |   | `4` |   | `8` | `8` | `12` |
| `Grand Total` | `4` | `2` | `6` | `1` | `8` | `9` | `15` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Flag', 'Side'], 'rows': ['Region'], 'values': ['Sum of Amount']}):

| `FALSE` | `TRUE` | `Row Labels` | `Buy` | `Sell` | `FALSE Total` | `Buy` | `Sell` | `TRUE Total` | `Grand Total` |
|---|---|---|---|---|---|---|---|---|---|
| `East` |   | `2` | `2` | `1` |   | `1` | `3` |   |   |
| `West` | `4` |   | `4` |   | `8` | `8` | `12` |   |   |
| `Grand Total` | `4` | `2` | `6` | `1` | `8` | `9` | `15` |   |   |

**The body, cell by cell, by row label:** the same, every cell.

### e13: item 13

Σ Values in Columns when the second Value Field is placed; headers 'Sum of Amount' under each column Item.

The records (`part-e/e13.csv`): `Region,Product,Amount,Qty` / `East,Apple,10,1` / `West,Pear,5,2`

Layout: rows ['Region'], columns ['Product'], values [('Amount', 'Sum', None), ('Qty', 'Sum', None)]

**Excel** (`A3:G8`; Value Fields [('Sum of Amount', 'General'), ('Sum of Qty', 'General')]):

|   | `Column Labels` |   |   |   |   |   |
|---|---|---|---|---|---|---|
|   | `Apple` |   | `Pear` |   | `Total Sum of Amount` | `Total Sum of Qty` |
| `Row Labels` | `Sum of Amount` | `Sum of Qty` | `Sum of Amount` | `Sum of Qty` |   |   |
| `East` | `10` | `1` |   |   | `10` | `1` |
| `West` |   |   | `5` | `2` | `5` | `2` |
| `Grand Total` | `10` | `1` | `5` | `2` | `15` | `3` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Product', 'Σ Values'], 'rows': ['Region'], 'values': ['Sum of Amount', 'Sum of Qty']}):

| `Apple` | `Pear` | `Grand Total` | `Row Labels` | `Sum of Amount` | `Sum of Qty` | `Sum of Amount` | `Sum of Qty` | `Sum of Amount` | `Sum of Qty` |
|---|---|---|---|---|---|---|---|---|---|
| `East` | `10` | `1` |   |   | `10` | `1` |   |   |   |
| `West` |   |   | `5` | `2` | `5` | `2` |   |   |   |
| `Grand Total` | `10` | `1` | `5` | `2` | `15` | `3` |   |   |   |

**The body, cell by cell, by row label:** the same, every cell.

### e14: item 14

Captions: Sum of Amount, Count of Region, a second Value Field of the same field and Aggregation Sum of Amount2.

The records (`part-e/e14.csv`): `Region,Amount` / `East,10` / `West,5`

Layout: rows [], columns [], values [('Amount', 'Sum', None), ('Region', 'Count', None), ('Amount', 'Sum', None)]

**Excel** (`A3:C4`; Value Fields [('Sum of Amount', 'General'), ('Count of Region', 'General'), ('Sum of Amount2', 'General')]):

| `Sum of Amount` | `Count of Region` | `Sum of Amount2` |
|---|---|---|
| `15` | `2` | `15` |

**ExPivot** (the Areas: {'filters': [], 'columns': ['Σ Values'], 'rows': [], 'values': ['Sum of Amount', 'Count of Region', 'Sum of Amount2']}):

| `Sum of Amount` | `Count of Region` | `Sum of Amount2` |
|---|---|---|
| `15` | `2` | `15` |

**The body, cell by cell, by row label:** the same, every cell.

