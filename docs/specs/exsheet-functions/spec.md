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
| `SUMIF` | Supported | — | Observed Windows cases; ASCII criteria and numeric comparisons. Culture-sensitive numeric text, text ordering, criteria longer than 255 characters, and resizing computed References are refused; ticket 08 |
| `SUMIFS` | Supported | — | As SUMIF; every criteria range must have the result range's shape |
| `COUNTIF` | Supported | — | As SUMIF; absent cells and formula-empty text remain distinct |
| `COUNTIFS` | Supported | — | As COUNTIF; every criteria range must have the same shape |
| `AVERAGEIF` | Supported | — | As SUMIF, including the implicit result footprint; no matching numbers is #DIV/0! |
| `AVERAGEIFS` | Supported | — | As SUMIFS; no matching numbers is #DIV/0! |
| `MAXIFS` | Supported | — | As SUMIFS; no matching numbers is 0 |
| `MINIFS` | Supported | — | As SUMIFS; no matching numbers is 0 |
| `COUNTBLANK` | Supported | — | Counts absent cells and formula-empty text, including whole-column ranges |
| `SUMPRODUCT` | Supported | — | Its arrays, operators on ranges included, reduced to one Value (ADR-0125) |
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
| `VLOOKUP` | Supported | — | Observed exact/approximate duplicates. Approximate searches require homogeneous, nonblank, error-free, sorted keys; exact text is ASCII; unobserved error-key outcomes are refused. Ticket 09 |
| `HLOOKUP` | Supported | — | As VLOOKUP, with the keys across the first row |
| `INDEX` | Supported | — | One value only. A row or column of 0, which returns a whole range, is `#VALUE!` until arrays get their ADR |
| `MATCH` | Supported | — | As VLOOKUP; descending equality takes the first duplicate. Descending nearest non-equal duplicate choice is refused pending observation |
| `XMATCH` | Supported | — | The modes as `XLOOKUP`'s |
| `CHOOSE` | Supported | — | |
| `ROW` | Supported | — | |
| `COLUMN` | Supported | — | |
| `ROWS` | Supported | — | |
| `COLUMNS` | Supported | — | |
| `OFFSET` | Supported | — | Volatile (ADR-0124): recalculated after every change, its computed Reference read as it stands |
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
| `MROUND` | Supported | — | Whole-number multiples; fractional multiples with nonzero same-sign operands are #VALUE! until Excel's binary midpoint algorithm is reproduced. Ticket 10 |
| `CEILING.MATH` | Supported | — | Observed sign, significance, mode and coercion; almost-integral division boundaries are #VALUE!, quotient underflow is #NUM!; ticket 10 |
| `FLOOR.MATH` | Supported | — | As CEILING.MATH; ticket 10 |
| `TRUNC` | Supported | — | |
| `POWER` | Supported | — | |
| `SQRT` | Supported | — | |
| `SIGN` | Supported | — | |
| `EXP` | Supported | — | |
| `LN` | Supported | — | |
| `LOG10` | Supported | — | |
| `LOG` | Supported | — | Base 10 when left out, through `LOG10`'s logarithm; base 1 is `#DIV/0!` until Excel is asked |
| `PI` | Supported | — | |

### Trigonometry

Angles are in radians. Microsoft documents a limit of 2^27 on the argument of `COT`, `CSC`, `SEC`,
`COTH`, `CSCH` and `SECH`; `SIN`, `COS` and `TAN` refuse an argument that large with `#NUM!` as
well, until Excel is asked (ticket 07).

| Function | Status | Priority | Notes |
|---|---|---|---|
| `SIN` | Supported | — | `#NUM!` from 2^27, until Excel is asked |
| `COS` | Supported | — | As `SIN` |
| `TAN` | Supported | — | As `SIN` |
| `COT` | Supported | — | `COT(0)` is `#DIV/0!`, as documented |
| `CSC` | Supported | — | At 0, `#DIV/0!` until Excel is asked |
| `SEC` | Supported | — | |
| `ASIN` | Supported | — | Outside −1 to 1, `#NUM!` |
| `ACOS` | Supported | — | As `ASIN` |
| `ATAN` | Supported | — | |
| `ATAN2` | Supported | — | `x_num` first, as Excel; both 0 is `#DIV/0!` |
| `ACOT` | Supported | — | From 0 to pi |
| `SINH` | Supported | — | Past what a number holds, `#NUM!` |
| `COSH` | Supported | — | As `SINH` |
| `TANH` | Supported | — | |
| `COTH` | Supported | — | At 0, `#DIV/0!` until Excel is asked |
| `CSCH` | Supported | — | As `COTH` |
| `SECH` | Supported | — | |
| `ASINH` | Supported | — | |
| `ACOSH` | Supported | — | Below 1, `#NUM!` |
| `ATANH` | Supported | — | Not strictly between −1 and 1, `#NUM!` |
| `ACOTH` | Supported | — | An absolute value not above 1 is `#NUM!`; the documentation names `#VALUE!` as well, so Excel is to be asked |
| `DEGREES` | Supported | — | |
| `RADIANS` | Supported | — | |

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
| `YEARFRAC` | Supported | — | All five bases, month ends, leap days and the 1900 date system; ticket 11 |
| `DATEDIF` | Supported | — | Y, M, D, YM; YD only before a completed anniversary. MD and longer YD intervals are #VALUE!; ticket 11 |
| `TIME` | Supported | — | |
| `HOUR` | Supported | — | |
| `MINUTE` | Supported | — | |
| `SECOND` | Supported | — | |
| `TODAY` | Supported | — | The Sheet Day: a fixed day, or the day in the Consumer's time zone, or else the browser's; `#GETTING_DATA` until one is known (ADR-0121, ADR-0122) |
| `NOW` | Supported | — | Volatile, and moved on each minute; the moment in the Sheet's zone (ADR-0124) |

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
| `SEARCH` | Supported | — | ASCII and the recorded BMP alphabet; supplementary and other characters are #VALUE!; ticket 12 |
| `UPPER` | Supported | — | ASCII and the recorded BMP casing alphabet; other characters are #VALUE!; ticket 12 |
| `LOWER` | Supported | — | As UPPER, plus the recorded uppercase Greek sigma contexts; other contexts are #VALUE! |
| `PROPER` | Supported | — | As UPPER, with only isolated sigma letters; other sigma contexts are #VALUE! |
| `REPT` | Supported | — | |
| `EXACT` | Supported | — | |
| `TEXT` | Supported | — | Its code in the invariant spelling, under every culture, shown as a cell format shows it (ADR-0120) |
| `VALUE` | Supported | — | Under the Sheet's culture, as the operators read text (ADR-0124) |
| `NUMBERVALUE` | Supported | — | Its separators are arguments, so it does not depend on a culture |

### Financial

| Function | Status | Priority | Notes |
|---|---|---|---|
| `PMT` | Supported | — | Excel is reported to give a General cell a currency format on entry; ExSheet gives none until that is observed |
| `PV` | Supported | — | |
| `FV` | Supported | — | |
| `NPV` | Supported | — | |
| `XNPV` | Supported | — | |
| `IRR` | Observe | P3 | Observation complete; candidate solvers do not reproduce Excel's root, stopping result and failures to 15 significant digits. Ticket 13 |
| `XIRR` | Observe | P3 | Observation complete; candidate solvers do not reproduce Excel's root, stopping result and failures to 15 significant digits. Ticket 13 |
| `RATE` | Observe | P3 | Observation complete; candidate solvers do not reproduce Excel's root, stopping result and failures to 15 significant digits. Ticket 13 |

### Dynamic arrays

A result of more than one cell spills (ADR-0125, which replaced ADR-0047's refusal).

| Function | Status | Priority | Notes |
|---|---|---|---|
| `FILTER` | Supported | — | Spills (ADR-0125); nothing kept is `#CALC!` without `if_empty` |
| `SORT` | Supported | — | Spills (ADR-0125); a blank or Error Value among the keys is refused until Excel is asked |
| `SORTBY` | Supported | — | Spills (ADR-0125); keys refused as `SORT`'s |
| `UNIQUE` | Supported | — | Spills (ADR-0125); a blank in the array is refused until Excel is asked |
| `SEQUENCE` | Supported | — | Spills (ADR-0125) |
| `TRANSPOSE` | Supported | — | Spills (ADR-0125) |
| `LET` | Decide | P3 | Observed through COM and real keys; Formula-local names, binding and entry grammar still need an ADR. Ticket 14 |

### Random

| Function | Status | Priority | Notes |
|---|---|---|---|
| `RAND` | Decide | P3 | Observed; reproducible random input, recalculation identity and persistence still need an ADR. Ticket 15 |
| `RANDBETWEEN` | Decide | P3 | As `RAND` |

## Order of work

1. **The P1 Ready functions** — done, ticket 01. Their `uncertain` cases go to the next Windows run.
2. **The P1 Observe functions** — observed on October 3 and admitted in tickets 08–09 within
   explicit domains: the criteria of the `SUMIF` family and legacy lookups.
3. **The P1 Decide functions, to the user.** Done: `TEXT`, ticket 02 (ADR-0120), and `TODAY`,
   ticket 03 (ADR-0121, ADR-0122).
4. **The P2 Ready functions** — done, ticket 04.
5. **The P3 Ready functions** — done, ticket 05. No function is Ready any more.
6. **`LOG` and the trigonometric functions** — done, ticket 07. Their `uncertain` cases go to the
   next Windows run.
7. **The October 3 Windows observation run is complete:** all 24 Observe functions and `LET`,
   `RAND`, `RANDBETWEEN`. Tickets 08–12 admit 21 functions within explicitly documented argument
   domains. Observation alone does not mark a function Supported.
8. **Financial iteration remains open**, ticket 13: the measured Excel results are available,
   but the tested candidate solvers do not meet the admission rule.
9. **LET and random functions need decisions**, tickets 14–15. Their observation is complete.
10. **Existing Supported functions' `uncertain` cases remain a separate Oracle backlog.** The
    October 3 run did not ask them, including LOG and trigonometry's remaining cases.

The retained evidence is `verification/2026-10-03-windows-functions/`. New corpus cases preserve
its IDs, typed fixtures and exact numeric expectations. A deliberate refusal keeps Excel's
observed result in `excelExpect` and names ADR-0047 in `engineDiffersByDecision`. The engine's
README lists the admitted domains; broadening one requires evidence, not a relaxed assertion.
