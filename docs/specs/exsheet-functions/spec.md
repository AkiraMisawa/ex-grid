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
| `SUMPRODUCT` | Ready | P2 | Its arrays, operators on ranges included, reduced to one Value (ADR-0125) |
| `PRODUCT` | Supported | — | |
| `MEDIAN` | Supported | — | |
| `LARGE` | Supported | — | |
| `SMALL` | Supported | — | |
| `RANK.EQ` | Supported | — | |
| `STDEV.S` | Supported | — | |
| `STDEV.P` | Supported | — | |
| `VAR.S` | Supported | — | |
| `VAR.P` | Supported | — | |
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
| `ISTEXT` | Supported | — | |
| `ISNA` | Supported | — | `#CIRC!` and `#GETTING_DATA` as `ISERROR` treats them |
| `IFS` | Supported | — | |
| `SWITCH` | Supported | — | |
| `XOR` | Supported | — | |
| `NA` | Supported | — | |
| `ERROR.TYPE` | Decide | P3 | `#CIRC!` and `#GETTING_DATA` have no number in Excel's table |

### Lookup and reference

| Function | Status | Priority | Notes |
|---|---|---|---|
| `XLOOKUP` | Supported | — | Binary search only over data sorted as the mode says (ADR-0047) |
| `VLOOKUP` | Observe | P1 | The default is the approximate match, a binary search: answered only over sorted data, as `XLOOKUP`'s binary search is, and `#VALUE!` otherwise. Which of equal keys it returns is to be observed |
| `HLOOKUP` | Observe | P2 | As `VLOOKUP` |
| `INDEX` | Supported | — | One value only. A row or column of 0, which returns a whole range, is `#VALUE!` until arrays get their ADR |
| `MATCH` | Observe | P1 | `match_type` 1 and −1 are binary searches, as `VLOOKUP` |
| `XMATCH` | Supported | — | The modes as `XLOOKUP`'s |
| `CHOOSE` | Supported | — | |
| `ROW` | Supported | — | |
| `COLUMN` | Supported | — | |
| `ROWS` | Supported | — | |
| `COLUMNS` | Supported | — | |
| `OFFSET` | Ready | P2 | Volatile (ADR-0124): recalculated after every change, its computed Reference read as it stands |
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
| `TRUNC` | Supported | — | |
| `POWER` | Supported | — | |
| `SQRT` | Supported | — | |
| `SIGN` | Supported | — | |
| `EXP` | Supported | — | |
| `LN` | Supported | — | |
| `LOG10` | Supported | — | |
| `PI` | Supported | — | |

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
| `WEEKDAY` | Supported | — | |
| `NETWORKDAYS` | Supported | — | |
| `WORKDAY` | Supported | — | |
| `DAYS` | Supported | — | |
| `YEARFRAC` | Observe | P3 | The documentation does not give each `basis`'s day count at month ends and leap years |
| `DATEDIF` | Observe | P3 | Microsoft documents `"MD"` as giving wrong results, so `"MD"` is refused |
| `TIME` | Supported | — | |
| `HOUR` | Supported | — | |
| `MINUTE` | Supported | — | |
| `SECOND` | Supported | — | |
| `TODAY` | Supported | — | The Sheet Day: a fixed day, or the day in the Consumer's time zone, or else the browser's; `#GETTING_DATA` until one is known (ADR-0121, ADR-0122) |
| `NOW` | Ready | P2 | Volatile, and moved on each minute; the moment in the Sheet's zone (ADR-0124) |

### Text

| Function | Status | Priority | Notes |
|---|---|---|---|
| `LEFT` | Supported | — | Counts UTF-16 code units, as Excel does |
| `RIGHT` | Supported | — | As `LEFT` |
| `MID` | Supported | — | As `LEFT` |
| `LEN` | Supported | — | As `LEFT` |
| `TRIM` | Supported | — | Only the space character, U+0020, as Excel trims |
| `CONCAT` | Supported | — | |
| `TEXTJOIN` | Supported | — | |
| `CONCATENATE` | Supported | — | |
| `SUBSTITUTE` | Supported | — | |
| `REPLACE` | Supported | — | |
| `FIND` | Supported | — | |
| `SEARCH` | Observe | P2 | Wildcards as `XLOOKUP`'s; case folding beyond ASCII is to be observed |
| `UPPER` | Observe | P2 | Case mapping beyond ASCII is to be observed |
| `LOWER` | Observe | P2 | As `UPPER` |
| `PROPER` | Observe | P3 | As `UPPER` |
| `REPT` | Supported | — | |
| `EXACT` | Supported | — | |
| `TEXT` | Supported | — | Its code in the invariant spelling, under every culture, shown as a cell format shows it (ADR-0120) |
| `VALUE` | Ready | P2 | Under the Sheet's culture, as the operators read text (ADR-0124) |
| `NUMBERVALUE` | Supported | — | Its separators are arguments, so it does not depend on a culture |

### Financial

| Function | Status | Priority | Notes |
|---|---|---|---|
| `PMT` | Supported | — | Excel is reported to give a General cell a currency format on entry; ExSheet gives none until that is observed |
| `PV` | Supported | — | |
| `FV` | Supported | — | |
| `NPV` | Supported | — | |
| `XNPV` | Supported | — | |
| `IRR` | Observe | P3 | Iterative. Admitted only if Excel's answer, to 15 significant digits, and its `#NUM!` cases are observed and reproduced; Microsoft does not document the iteration |
| `XIRR` | Observe | P3 | As `IRR` |
| `RATE` | Observe | P3 | As `IRR` |

### Dynamic arrays

A result of more than one cell spills (ADR-0125, which replaced ADR-0047's refusal).

| Function | Status | Priority | Notes |
|---|---|---|---|
| `FILTER` | Ready | P2 | Spills (ADR-0125) |
| `SORT` | Ready | P3 | Spills (ADR-0125) |
| `SORTBY` | Ready | P3 | Spills (ADR-0125) |
| `UNIQUE` | Ready | P2 | Spills (ADR-0125) |
| `SEQUENCE` | Ready | P3 | Spills (ADR-0125) |
| `TRANSPOSE` | Ready | P3 | Spills (ADR-0125) |
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
3. **The P1 Decide functions, to the user.** Done: `TEXT`, ticket 02 (ADR-0120), and `TODAY`,
   ticket 03 (ADR-0121, ADR-0122).
4. **The P2 Ready functions** — done, ticket 04.
5. **The P3 Ready functions** — done, ticket 05. No function is Ready any more.
6. **The Observe functions, through one Windows run**, P1 first, and the `uncertain` cases of
   tickets 01 to 05 in the same run. Then the Decide functions, P2 first, each to the user.
