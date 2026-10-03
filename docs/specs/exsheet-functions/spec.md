# ExSheet's function catalogue

Status: ready-for-agent

The functions ExSheet's engine supports, and the queue of those it may support next, each with its
status and a priority. **ADR-0047 decides whether a function is admitted; this file only orders the
queue.** A function is admitted when it matches Excel, edge cases included, or gives an Error Value
for the argument values where it cannot. It never gives a different answer. A priority does not
change that rule, and a function listed here is not a promise.

`tests/ExSheet.Engine.Tests/FunctionCatalogueTests.cs` reads this file: the rows marked
**Supported** must be exactly `DeclaredFunction.All`. Admitting a function without changing its row
here fails the build, and so does changing a row without admitting the function.

## Status

| Status | Meaning | What moves it on |
|---|---|---|
| **Supported** | Declared in `FunctionLibrary`, with a table of Excel's results in the tests | — |
| **Ready** | Microsoft's documentation pins its behaviour, blanks, text, booleans and Error Values included. Its tests can be written from that documentation now | A ticket |
| **Observe** | The documentation leaves cases open that a user meets. A Windows run has to ask Excel before those tests are written | A Windows run (`docs/specs/exsheet/verify-on-windows-N.md`) |
| **Decide** | It cannot be admitted under the existing ADRs. An ADR has to be written first, with the user | An ADR |

## Priority

- **P1, next.** Common in the sheets ExSheet is for: budgets, P&L, pricing and cost reports
  (ExSheet spec), and in sheets that users bring over from Excel.
- **P2, soon.** Common, but each one has a usual workaround in the functions above it.
- **P3, later.** Specialised, or needs work out of proportion to how often it is used.

The priority says how much a function is wanted. The status says what stands in its way. A P1
function marked **Decide** waits for its ADR, and the P1 functions marked **Ready** go first.

## Admitting a function

1. A ticket under `issues/`, numbered within this spec, naming the functions it admits.
2. A table of Excel's results in `tests/ExSheet.Engine.Tests/FunctionTests.cs`, and the cases in
   `tests/ExSheet.Engine.Tests/ExcelCases/` that the next Windows run checks against Excel.
3. The declaration in `src/ExSheet.Engine/Formulas/Functions.cs`, with Excel's argument names and
   signature (completion and the argument hint read them; ADR-0051).
4. Its row here becomes **Supported**. ADR-0047's list gets a dated addition naming the function,
   and `The_declared_functions_are_exactly_adr_0047s_set` takes the new name.
5. The table in `src/ExSheet.Engine/README.md`, and any value an argument takes from a fixed list
   (`DeclaredFunction.ValuesOf`, ADR-0058).

## Catalogue

### Aggregation

| Function | Status | Priority | Notes |
|---|---|---|---|
| `SUM` | Supported | — | |
| `AVERAGE` | Supported | — | |
| `MIN` | Supported | — | |
| `MAX` | Supported | — | |
| `COUNT` | Supported | — | |
| `COUNTA` | Supported | — | |
| `SUMIF` | Observe | P1 | Criteria are text that Excel parses. To be observed: `"="` and `"<>"` against blanks, a number typed as text, wildcards and `~`. A criteria number whose reading depends on the culture (`">1,5"`) may be refused with `#VALUE!` (ADR-0047, admitted with some argument values refused). One criteria parser serves the whole family |
| `SUMIFS` | Observe | P1 | As `SUMIF`; ranges of different shapes are `#VALUE!` |
| `COUNTIF` | Observe | P1 | As `SUMIF` |
| `COUNTIFS` | Observe | P1 | As `SUMIF` |
| `AVERAGEIF` | Observe | P2 | As `SUMIF` |
| `AVERAGEIFS` | Observe | P2 | As `SUMIF` |
| `MAXIFS` | Observe | P2 | As `SUMIF` |
| `MINIFS` | Observe | P2 | As `SUMIF` |
| `COUNTBLANK` | Observe | P2 | Whether `""` returned by a Formula counts as blank is to be observed |
| `SUMPRODUCT` | Decide | P2 | Arguments given as ranges, `SUMPRODUCT(A1:A3, B1:B3)`, could be Ready. Its common form `SUMPRODUCT((A1:A3="x")*B1:B3)` applies an operator to a range, which ADR-0047 refuses until arrays get their ADR |
| `PRODUCT` | Ready | P2 | |
| `MEDIAN` | Ready | P2 | |
| `LARGE` | Ready | P2 | |
| `SMALL` | Ready | P2 | |
| `RANK.EQ` | Ready | P3 | |
| `STDEV.S` | Ready | P3 | |
| `STDEV.P` | Ready | P3 | |
| `VAR.S` | Ready | P3 | |
| `VAR.P` | Ready | P3 | |
| `SUBTOTAL` | Decide | P3 | Its meaning turns on hidden rows, and a Sheet has none yet (ADR-0046) |
| `AGGREGATE` | Decide | P3 | As `SUBTOTAL` |

### Logic and errors

| Function | Status | Priority | Notes |
|---|---|---|---|
| `IF` | Supported | — | |
| `IFERROR` | Supported | — | |
| `ISERROR` | Supported | — | |
| `AND` | Supported | — | |
| `OR` | Supported | — | |
| `NOT` | Supported | — | |
| `IFNA` | Supported | — | The partner of `XLOOKUP` and `MATCH`. Like `IFERROR`, it catches neither `#CIRC!` nor `#GETTING_DATA` (ADR-0047, ADR-0049) |
| `ISBLANK` | Supported | — | |
| `ISNUMBER` | Supported | — | |
| `ISTEXT` | Ready | P2 | |
| `ISNA` | Ready | P2 | `#CIRC!` and `#GETTING_DATA` as `ISERROR` treats them |
| `IFS` | Ready | P2 | |
| `SWITCH` | Ready | P2 | |
| `XOR` | Ready | P3 | |
| `NA` | Ready | P2 | |
| `ERROR.TYPE` | Decide | P3 | `#CIRC!` and `#GETTING_DATA` have no number in Excel's table |

### Lookup and reference

| Function | Status | Priority | Notes |
|---|---|---|---|
| `XLOOKUP` | Supported | — | Binary search only over data sorted as the mode says (ADR-0047) |
| `VLOOKUP` | Observe | P1 | The default is the approximate match, a binary search: answered only over sorted data, as `XLOOKUP`'s binary search is, and `#VALUE!` otherwise. Which of equal keys it returns is to be observed |
| `HLOOKUP` | Observe | P2 | As `VLOOKUP` |
| `INDEX` | Supported | — | One value only. A row or column of 0, which returns a whole range, is `#VALUE!` until arrays get their ADR |
| `MATCH` | Observe | P1 | `match_type` 1 and −1 are binary searches, as `VLOOKUP` |
| `XMATCH` | Ready | P2 | The modes as `XLOOKUP`'s |
| `CHOOSE` | Ready | P2 | |
| `ROW` | Ready | P2 | |
| `COLUMN` | Ready | P2 | |
| `ROWS` | Ready | P3 | |
| `COLUMNS` | Ready | P3 | |
| `OFFSET` | Decide | P2 | A Reference computed at recalculation: the dependency graph reads References from the Formula's text (ADR-0047), and Excel makes `OFFSET` volatile |
| `INDIRECT` | Decide | P3 | As `OFFSET`, and its text names cells that an insertion does not rewrite |

### Math and rounding

| Function | Status | Priority | Notes |
|---|---|---|---|
| `ROUND` | Supported | — | |
| `ROUNDUP` | Supported | — | |
| `ROUNDDOWN` | Supported | — | |
| `ABS` | Supported | — | |
| `INT` | Supported | — | |
| `MOD` | Supported | — | Excel's sign follows the divisor |
| `MROUND` | Observe | P2 | Halfway cases in binary doubles are to be observed |
| `CEILING.MATH` | Observe | P2 | As `MROUND` |
| `FLOOR.MATH` | Observe | P2 | As `MROUND` |
| `TRUNC` | Ready | P2 | |
| `POWER` | Ready | P2 | |
| `SQRT` | Ready | P2 | |
| `SIGN` | Ready | P3 | |
| `EXP` | Ready | P3 | |
| `LN` | Ready | P3 | |
| `LOG10` | Ready | P3 | |
| `PI` | Ready | P3 | |

### Date and time

Dates are serial day numbers in Excel's 1900 date system, 29 February 1900 included (ADR-0047).

| Function | Status | Priority | Notes |
|---|---|---|---|
| `DATE` | Supported | — | Months and days out of range roll over, as Excel's do |
| `YEAR` | Supported | — | |
| `MONTH` | Supported | — | |
| `DAY` | Supported | — | |
| `EOMONTH` | Supported | — | |
| `EDATE` | Supported | — | |
| `WEEKDAY` | Ready | P2 | |
| `NETWORKDAYS` | Ready | P2 | |
| `WORKDAY` | Ready | P2 | |
| `DAYS` | Ready | P2 | |
| `YEARFRAC` | Observe | P3 | The documentation does not give each `basis`'s day count at month ends and leap years |
| `DATEDIF` | Observe | P3 | Microsoft documents `"MD"` as giving wrong results, so `"MD"` is refused |
| `TIME` | Ready | P3 | |
| `HOUR` | Ready | P3 | |
| `MINUTE` | Ready | P3 | |
| `SECOND` | Ready | P3 | |
| `TODAY` | Decide | P1 | Its value depends on when the Sheet is recalculated. A Sheet Document holds Entries and every Value is recomputed (ADR-0048), so a saved sheet changes its answer from day to day. It also needs a volatile recalculation that ADR-0047 does not have, and a time zone |
| `NOW` | Decide | P2 | As `TODAY` |

### Text

| Function | Status | Priority | Notes |
|---|---|---|---|
| `LEFT` | Supported | — | Counts UTF-16 code units, as Excel does |
| `RIGHT` | Supported | — | As `LEFT` |
| `MID` | Supported | — | As `LEFT` |
| `LEN` | Supported | — | As `LEFT` |
| `TRIM` | Supported | — | Only the space character, U+0020, as Excel trims |
| `CONCAT` | Supported | — | |
| `TEXTJOIN` | Ready | P2 | |
| `CONCATENATE` | Ready | P2 | |
| `SUBSTITUTE` | Ready | P2 | |
| `REPLACE` | Ready | P2 | |
| `FIND` | Ready | P2 | |
| `SEARCH` | Observe | P2 | Wildcards as `XLOOKUP`'s; case folding beyond ASCII is to be observed |
| `UPPER` | Observe | P2 | Case mapping beyond ASCII is to be observed |
| `LOWER` | Observe | P2 | As `UPPER` |
| `PROPER` | Observe | P3 | As `UPPER` |
| `REPT` | Ready | P3 | |
| `EXACT` | Ready | P3 | |
| `TEXT` | Decide | P1 | Excel reads its format code in the system's locale (`"yyyy"` is `"JJJJ"` in German). ADR-0047 writes every Formula in one spelling, so which codes it reads is a decision; `NumberFormat` already renders the codes |
| `VALUE` | Decide | P2 | Excel parses the text in the system's locale. ExSheet's culture is the Sheet's (ADR-0048) |
| `NUMBERVALUE` | Ready | P3 | Its separators are arguments, so it does not depend on a culture |

### Financial

| Function | Status | Priority | Notes |
|---|---|---|---|
| `PMT` | Ready | P2 | |
| `PV` | Ready | P2 | |
| `FV` | Ready | P2 | |
| `NPV` | Ready | P2 | |
| `XNPV` | Ready | P3 | |
| `IRR` | Observe | P3 | Iterative. Admitted only if Excel's answer, to 15 significant digits, and its `#NUM!` cases are observed and reproduced; Microsoft does not document the iteration |
| `XIRR` | Observe | P3 | As `IRR` |
| `RATE` | Observe | P3 | As `IRR` |

### Dynamic arrays

ADR-0047 refuses a result of more than one cell with `#VALUE!` and gives spilling an ADR of its
own. Every function here waits for that ADR.

| Function | Status | Priority | Notes |
|---|---|---|---|
| `FILTER` | Decide | P2 | |
| `SORT` | Decide | P3 | |
| `SORTBY` | Decide | P3 | |
| `UNIQUE` | Decide | P2 | |
| `SEQUENCE` | Decide | P3 | |
| `TRANSPOSE` | Decide | P3 | |
| `LET` | Decide | P3 | Not an array function, but names inside a Formula are new grammar (ADR-0047) |

### Random

| Function | Status | Priority | Notes |
|---|---|---|---|
| `RAND` | Decide | P3 | An answer that changes on every recalculation, against "an outcome never depends on timing" |
| `RANDBETWEEN` | Decide | P3 | As `RAND` |

## Order of work

1. **The P1 Ready functions** — done, ticket 01. Their `uncertain` cases go to the next Windows run.
2. **The P1 Observe functions, through one Windows run.** The criteria of the `SUMIF` family, and
   the approximate match of `VLOOKUP` and `MATCH`.
3. **The P1 Decide functions, to the user.** `TODAY` (volatility, and what a saved document means),
   and `TEXT` (which culture's format codes).
4. P2, then P3, in the same way.
