# ExSheet's function catalogue

Status: done — but for [ticket 13](issues/13-financial-iteration.md) (`needs-info`): `IRR`, `XIRR` and
`RATE` are Blocked, since no algorithm reproduces Excel's, and are not pursued for now; and `NPV` is
Blocked with them since the area pass of 2026-10-12, since no evaluation searched gives every double
Excel gave for it (ADR-0202's note of that date). And but for
tickets [07](issues/07-log-and-trigonometry.md) and [16](issues/16-subtotal-and-aggregate.md)
(`ready-for-human`): the `uncertain` cases the seventeenth run's answers led to are asked by an
`oracle.ps1 -Area` run before the tag (ADR-0208). Tickets 14 to 18,
decided with the user on 2026-10-10 (item 11 below), are built and done (item 12); their questions, with
every other ticket's `uncertain` cases, are corpus cases the sign-off's Windows run puts to Excel, but
for two of ticket 15's that the corpus cannot ask (what an undo does to a draw, and whether a rename draws
again), which that run asks by hand. *(The seventeenth run asked them, 2026-10-11: an undo and a rename
each draw again in Excel; ExSheet's undo already did, and its rename now does, ADR-0180's note of
2026-10-12.)*
*(Set on 2026-10-11.)*

The functions ExSheet's engine supports, and the queue of those it may support next, each with its
status and a priority. **ADR-0047 decides whether a function is admitted; this file only orders the
queue.** A function is admitted when it matches Excel, edge cases included, or gives an Error Value
for the argument values where it cannot. It never gives a different answer. A priority does not
change that rule, and a function listed here is not a promise.

`tests/ExSheet.Engine.Tests/FunctionCatalogueTests.cs` reads this file: the rows marked
**Supported** must be exactly `DeclaredFunction.All`. Admitting a function without changing its row
here fails the build, and so does changing a row without admitting the function. It also reads the
file's list of [Excel's functions not in the queue](#excels-functions-not-in-the-queue): each name is
listed once, under its letter, and has no row; and `LetTests` binds every name the rows and the list
hold that is a name in shape as a `LET` name, as Excel's keyboard took `SUM`, `IRR` and `N` (ADR-0181,
the seventeenth run).

## Status

| Status | Meaning | What moves it on |
|---|---|---|
| **Supported** | Declared in `FunctionLibrary`, with a table of Excel's results in the tests | — |
| **Ready** | Microsoft's documentation pins its behaviour, blanks, text, booleans and Error Values included, or an ADR does with the observations. Its tests can be written now | A ticket |
| **Observe** | The documentation leaves cases open that a user meets. A Windows run has to ask Excel before those tests are written | A Windows run (`docs/specs/exsheet/verify-on-windows-N.md`) |
| **Decide** | It cannot be admitted under the existing ADRs. An ADR has to be written first, with the user | An ADR |
| **Blocked** | Observed in Excel, but nothing yet reproduces every observed case to the digits ADR-0047 compares | An implementation that does |

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
| `LARGE` | Supported | — | A `k` that is not whole is rounded up, as Excel's (ADR-0047, 2026-10-12) |
| `SMALL` | Supported | — | A `k` that is not whole is `#VALUE!` until observed |
| `RANK.EQ` | Supported | — | A `ref` that can never be a Reference is refused on entry |
| `STDEV.S` | Supported | — | |
| `STDEV.P` | Supported | — | |
| `VAR.S` | Supported | — | |
| `VAR.P` | Supported | — | |
| `SUBTOTAL` | Supported | — | Admitted while a Sheet cannot hide a row, behind a test that fails when one can; a cell whose Formula calls `SUBTOTAL` or `AGGREGATE` anywhere is nested and passed over, as Excel does (ADR-0047, 2026-10-10 and 2026-10-12). Ticket 16 |
| `AGGREGATE` | Supported | — | As `SUBTOTAL`; options 0 to 3 pass over a nested cell and 4 to 7 count it, and a function number for a function the engine does not support (13, 16 to 19) is `#VALUE!`. Ticket 16 |

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
| `ERROR.TYPE` | Supported | — | `#GETTING_DATA` and `#CIRC!` are given back, never numbered; Excel's newer Error Values take Excel's numbers, 9 to 14, read by the seventeenth Windows run (ADR-0047, 2026-10-10 and its note of 2026-10-12). Ticket 17 |

### Lookup and reference

| Function | Status | Priority | Notes |
|---|---|---|---|
| `XLOOKUP` | Supported | — | Binary search only over data sorted as the mode says (ADR-0047) |
| `VLOOKUP` | Supported | — | Observed exact/approximate duplicates. Approximate searches require homogeneous, nonblank, error-free, sorted keys; exact text is ASCII; unobserved error-key outcomes are refused. Ticket 09 |
| `HLOOKUP` | Supported | — | As VLOOKUP, with the keys across the first row |
| `INDEX` | Supported | — | Spills a whole row or column for a row or a column of 0 (ADR-0125); over a range of several rows and several columns, a Spill Range included, `column_num` left out is `#REF!`, as Excel's is (seventeenth run) |
| `MATCH` | Supported | — | As VLOOKUP; descending equality takes the first duplicate. Descending nearest non-equal duplicate choice is refused pending observation |
| `XMATCH` | Supported | — | The modes as `XLOOKUP`'s, and its binary search refused where `XLOOKUP`'s is (Excel's answer recorded, XMATCH-008) |
| `CHOOSE` | Supported | — | |
| `ROW` | Supported | — | |
| `COLUMN` | Supported | — | |
| `ROWS` | Supported | — | |
| `COLUMNS` | Supported | — | |
| `OFFSET` | Supported | — | Volatile (ADR-0124): recalculated after every change, its computed Reference read as it stands |
| `INDIRECT` | Supported | — | Volatile, as `OFFSET`; A1-style text naming a place on the same Sheet, not rewritten by an insertion; R1C1 text (each R and C a number, an offset in brackets from the Formula's own cell, or none, the own row or column; a cell, a whole row or column, the rectangle two parts of the same kind span, or one cell's Spill Range with `#`), a Linked Table's column as structured text, and `A1#`, as the seventeenth run and the two area passes observed and by the rule they settle; `#VALUE!` until Excel is asked for R1C1 text with a row or column number 0, an offset past the Sheet's edge, or `#` after a range, a row or a column, and by decision for a declared Linked Table's bare name (ADR-0049's rule 2); `#REF!` for text that names no place, R1C1 parts of two kinds joined included, and, as the engine's answer until Excel is asked, for an R1C1 row or column number past the Sheet's edge and A1 text with `#` after a range (ADR-0124, 2026-10-10 and 2026-10-12). Ticket 18 |

### Math and rounding

| Function | Status | Priority | Notes |
|---|---|---|---|
| `ROUND` | Supported | — | |
| `ROUNDUP` | Supported | — | |
| `ROUNDDOWN` | Supported | — | |
| `ABS` | Supported | — | |
| `INT` | Supported | — | |
| `MOD` | Supported | — | Excel's sign follows the divisor; `#NUM!` from a quotient of 1,125,900,000,000 |
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
| `LOG` | Supported | — | Base 10 when left out, through `LOG10`'s logarithm; base 1 is `#DIV/0!`, as Excel's (the seventeenth run, LOG-007) |
| `PI` | Supported | — | |

### Trigonometry

Angles are in radians, reduced by pi as Excel holds it, to 64 bits (ADR-0047, 2026-10-12). Microsoft
documents a limit of 2^27 on the argument of `COT`, `CSC`, `SEC`, `COTH`, `CSCH` and `SECH`; Excel
applies it to `SIN`, `COS`, `TAN`, `COT`, `CSC` and `SEC` (`#NUM!`) and answers `COTH`, `CSCH` and
`SECH` past it, as the engine does (ticket 07; observed 2026-10-11).

| Function | Status | Priority | Notes |
|---|---|---|---|
| `SIN` | Supported | — | `#NUM!` from 2^27, as Excel's |
| `COS` | Supported | — | As `SIN` |
| `TAN` | Supported | — | As `SIN` |
| `COT` | Supported | — | `COT(0)` is `#DIV/0!`, as documented |
| `CSC` | Supported | — | At 0, `#DIV/0!`, as Excel's |
| `SEC` | Supported | — | |
| `ASIN` | Supported | — | Outside −1 to 1, `#NUM!` |
| `ACOS` | Supported | — | As `ASIN` |
| `ATAN` | Supported | — | |
| `ATAN2` | Supported | — | `x_num` first, as Excel; both 0 is `#DIV/0!` |
| `ACOT` | Supported | — | From 0 to pi |
| `SINH` | Supported | — | Past what a number holds, `#NUM!` |
| `COSH` | Supported | — | As `SINH` |
| `TANH` | Supported | — | |
| `COTH` | Supported | — | At 0, `#DIV/0!`, as Excel's; no limit at 2^27, as Excel's |
| `CSCH` | Supported | — | As `COTH` |
| `SECH` | Supported | — | No limit at 2^27, as Excel's |
| `ASINH` | Supported | — | |
| `ACOSH` | Supported | — | Below 1, `#NUM!` |
| `ATANH` | Supported | — | Not strictly between −1 and 1, `#NUM!` |
| `ACOTH` | Supported | — | An absolute value not above 1 is `#NUM!`, as Excel's (the documentation names `#VALUE!` as well) |
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
| `PMT` | Supported | — | At a rate other than 0, an `nper` that is not a whole number from 0 is #VALUE! (ADR-0047, note of 2026-10-12). Excel gives a General cell its currency format on entry (the area pass of 2026-10-12 read `$#,##0.00_);[Red]($#,##0.00)` for PMT, PV, FV and NPV); ExSheet gives none |
| `PV` | Supported | — | `type` is read as a truth value; an `nper` that is not a whole number, or is negative, is answered where every evaluation Excel's observed doubles leave gives the same double, and is #VALUE! until Excel is asked where they part (half a period, FV-016; a negative fraction, FV-022), at a rate of -1 when negative (PV-012), and at a rate below -1 when not a whole number (PV-011) |
| `FV` | Supported | — | As `PV`; with both a present value and a payment, #VALUE! where the unfused sum and a fused multiply-add part (FV-014, FV-015), but at the end of each period with a whole `nper` from 0, which answers the unfused sum as before (FV-019 asks) |
| `NPV` | Blocked | P3 | Observed: no evaluation searched gives every double Excel gave, to the last bit (NPV-001 to 008, `spikes/exsheet-financial-evaluation/`). Refused for 0.1.0 as IRR is (ADR-0202, note of 2026-10-12). Ticket 13 |
| `XNPV` | Supported | — | #VALUE! where the days over 365 or times 1/365, and at whole years e^(years ln(1+rate)), squaring or a power, part (XNPV-005, 006, 011), and at a rate of -1 or below (XNPV-007 to 010), until Excel is asked. Other evaluations give both observed doubles too and part from these at most inputs; whether XNPV answers there is the user's question (ADR-0202, ticket 13; XNPV-012 to 017 ask) |
| `IRR` | Blocked | P3 | Observation complete; candidate solvers do not reproduce Excel's root, stopping result and failures to 15 significant digits. Not pursued for now (ADR-0047, 2026-10-10). Ticket 13 |
| `XIRR` | Blocked | P3 | Observation complete; candidate solvers do not reproduce Excel's root, stopping result and failures to 15 significant digits. Not pursued for now (ADR-0047, 2026-10-10). Ticket 13 |
| `RATE` | Blocked | P3 | Observation complete; candidate solvers do not reproduce Excel's root, stopping result and failures to 15 significant digits. Not pursued for now (ADR-0047, 2026-10-10). Ticket 13 |

### Dynamic arrays

A result of more than one cell spills (ADR-0125, which replaced ADR-0047's refusal).

| Function | Status | Priority | Notes |
|---|---|---|---|
| `FILTER` | Supported | — | Spills (ADR-0125); nothing kept is `#CALC!` without `if_empty` |
| `SORT` | Supported | — | Spills (ADR-0125); a blank or Error Value among the keys is refused until Excel is asked; Excel's Anchor recorded (SORT-011), the refusal kept until a run reads the rest |
| `SORTBY` | Supported | — | Spills (ADR-0125); keys refused as `SORT`'s; Excel's Anchor recorded (SORT-011), the refusal kept until a run reads the rest |
| `UNIQUE` | Supported | — | Spills (ADR-0125); a blank in the array is refused until Excel is asked; Excel's Anchor recorded (UNIQUE-009), the refusal kept until a run reads the rest |
| `SEQUENCE` | Supported | — | Spills (ADR-0125) |
| `TRANSPOSE` | Supported | — | Spills (ADR-0125) |
| `LET` | Supported | — | Names bound inside one Formula, as Excel's keyboard accepts them; a function's name, declared or not, and an R1C1 shape that is no A1 Reference are names, a value may be left empty, and `TRUE`, `FALSE` and a name that reads as an A1 Reference are refused on entry (ADR-0181, the seventeenth run). Ticket 14 |

### Random

| Function | Status | Priority | Notes |
|---|---|---|---|
| `RAND` | Supported | — | Draws from the Sheet Seed, and draws again at each change, never on the clock (ADR-0180). Ticket 15 |
| `RANDBETWEEN` | Supported | — | As `RAND`; a fractional bound and a bound past 2^53 are `#VALUE!` (ADR-0180). Ticket 15 |

## Excel's functions not in the queue

The rest of Excel's worksheet functions, as Microsoft documents them. None of them is queued: each
is `#NAME?` (ADR-0047), and none has a status or a priority until a row above catalogues it.
*(They were listed because a `LET` name was refused if it was the name of any of Excel's functions
(ADR-0181's note of 2026-10-11). The seventeenth Windows run saw Excel's keyboard take `SUM`, `IRR`
and `N` as `LET` names, so none is refused now. The list stays as the catalogue's account of what
Excel has beyond its rows, and `LetTests` binds each name-shaped one as a `LET` name.)*

`FunctionCatalogueTests` reads this list with the rows above: each name is listed once, under its own
letter, in order, and has no row. Cataloguing a function moves its name from here to a row; a
function Excel adds is listed here.

- **A**: `ACCRINT`, `ACCRINTM`, `ADDRESS`, `AMORDEGRC`, `AMORLINC`, `ARABIC`, `AREAS`,
  `ARRAYTOTEXT`, `ASC`, `AVEDEV`, `AVERAGEA`
- **B**: `BAHTTEXT`, `BASE`, `BESSELI`, `BESSELJ`, `BESSELK`, `BESSELY`, `BETA.DIST`, `BETA.INV`,
  `BETADIST`, `BETAINV`, `BIN2DEC`, `BIN2HEX`, `BIN2OCT`, `BINOM.DIST`, `BINOM.DIST.RANGE`,
  `BINOM.INV`, `BINOMDIST`, `BITAND`, `BITLSHIFT`, `BITOR`, `BITRSHIFT`, `BITXOR`, `BYCOL`, `BYROW`
- **C**: `CALL`, `CEILING`, `CEILING.PRECISE`, `CELL`, `CHAR`, `CHIDIST`, `CHIINV`, `CHISQ.DIST`,
  `CHISQ.DIST.RT`, `CHISQ.INV`, `CHISQ.INV.RT`, `CHISQ.TEST`, `CHITEST`, `CHOOSECOLS`, `CHOOSEROWS`,
  `CLEAN`, `CODE`, `COMBIN`, `COMBINA`, `COMPLEX`, `CONFIDENCE`, `CONFIDENCE.NORM`, `CONFIDENCE.T`,
  `CONVERT`, `COPILOT`, `CORREL`, `COUPDAYBS`, `COUPDAYS`, `COUPDAYSNC`, `COUPNCD`, `COUPNUM`,
  `COUPPCD`, `COVAR`, `COVARIANCE.P`, `COVARIANCE.S`, `CRITBINOM`, `CUBEKPIMEMBER`, `CUBEMEMBER`,
  `CUBEMEMBERPROPERTY`, `CUBERANKEDMEMBER`, `CUBESET`, `CUBESETCOUNT`, `CUBEVALUE`, `CUMIPMT`,
  `CUMPRINC`
- **D**: `DATEVALUE`, `DAVERAGE`, `DAYS360`, `DB`, `DBCS`, `DCOUNT`, `DCOUNTA`, `DDB`, `DEC2BIN`,
  `DEC2HEX`, `DEC2OCT`, `DECIMAL`, `DELTA`, `DETECTLANGUAGE`, `DEVSQ`, `DGET`, `DISC`, `DMAX`,
  `DMIN`, `DOLLAR`, `DOLLARDE`, `DOLLARFR`, `DPRODUCT`, `DROP`, `DSTDEV`, `DSTDEVP`, `DSUM`,
  `DURATION`, `DVAR`, `DVARP`
- **E**: `ECMA.CEILING`, `EFFECT`, `ENCODEURL`, `ERF`, `ERF.PRECISE`, `ERFC`, `ERFC.PRECISE`,
  `EUROCONVERT`, `EVEN`, `EXPAND`, `EXPON.DIST`, `EXPONDIST`
- **F**: `F.DIST`, `F.DIST.RT`, `F.INV`, `F.INV.RT`, `F.TEST`, `FACT`, `FACTDOUBLE`, `FALSE`,
  `FDIST`, `FIELDVALUE`, `FILTERXML`, `FINDB`, `FINV`, `FISHER`, `FISHERINV`, `FIXED`, `FLOOR`,
  `FLOOR.PRECISE`, `FORECAST`, `FORECAST.ETS`, `FORECAST.ETS.CONFINT`, `FORECAST.ETS.SEASONALITY`,
  `FORECAST.ETS.STAT`, `FORECAST.LINEAR`, `FORMULATEXT`, `FREQUENCY`, `FTEST`, `FVSCHEDULE`
- **G**: `GAMMA`, `GAMMA.DIST`, `GAMMA.INV`, `GAMMADIST`, `GAMMAINV`, `GAMMALN`, `GAMMALN.PRECISE`,
  `GAUSS`, `GCD`, `GEOMEAN`, `GESTEP`, `GETPIVOTDATA`, `GROUPBY`, `GROWTH`
- **H**: `HARMEAN`, `HEX2BIN`, `HEX2DEC`, `HEX2OCT`, `HSTACK`, `HYPERLINK`, `HYPGEOM.DIST`,
  `HYPGEOMDIST`
- **I**: `IMABS`, `IMAGE`, `IMAGINARY`, `IMARGUMENT`, `IMCONJUGATE`, `IMCOS`, `IMCOSH`, `IMCOT`,
  `IMCSC`, `IMCSCH`, `IMDIV`, `IMEXP`, `IMLN`, `IMLOG10`, `IMLOG2`, `IMPOWER`, `IMPRODUCT`,
  `IMREAL`, `IMSEC`, `IMSECH`, `IMSIN`, `IMSINH`, `IMSQRT`, `IMSUB`, `IMSUM`, `IMTAN`, `INFO`,
  `INTERCEPT`, `INTRATE`, `IPMT`, `ISERR`, `ISEVEN`, `ISFORMULA`, `ISLOGICAL`, `ISNONTEXT`,
  `ISO.CEILING`, `ISODD`, `ISOMITTED`, `ISOWEEKNUM`, `ISPMT`, `ISREF`
- **J**: `JIS`
- **K**: `KURT`
- **L**: `LAMBDA`, `LCM`, `LEFTB`, `LENB`, `LINEST`, `LOGEST`, `LOGINV`, `LOGNORM.DIST`,
  `LOGNORM.INV`, `LOGNORMDIST`, `LOOKUP`
- **M**: `MAKEARRAY`, `MAP`, `MAXA`, `MDETERM`, `MDURATION`, `MIDB`, `MINA`, `MINVERSE`, `MIRR`,
  `MMULT`, `MODE`, `MODE.MULT`, `MODE.SNGL`, `MULTINOMIAL`, `MUNIT`
- **N**: `N`, `NEGBINOM.DIST`, `NEGBINOMDIST`, `NETWORKDAYS.INTL`, `NOMINAL`, `NORM.DIST`,
  `NORM.INV`, `NORM.S.DIST`, `NORM.S.INV`, `NORMDIST`, `NORMINV`, `NORMSDIST`, `NORMSINV`, `NPER`
- **O**: `OCT2BIN`, `OCT2DEC`, `OCT2HEX`, `ODD`, `ODDFPRICE`, `ODDFYIELD`, `ODDLPRICE`, `ODDLYIELD`
- **P**: `PDURATION`, `PEARSON`, `PERCENTILE`, `PERCENTILE.EXC`, `PERCENTILE.INC`, `PERCENTOF`,
  `PERCENTRANK`, `PERCENTRANK.EXC`, `PERCENTRANK.INC`, `PERMUT`, `PERMUTATIONA`, `PHI`, `PHONETIC`,
  `PIVOTBY`, `POISSON`, `POISSON.DIST`, `PPMT`, `PRICE`, `PRICEDISC`, `PRICEMAT`, `PROB`, `PY`
- **Q**: `QUARTILE`, `QUARTILE.EXC`, `QUARTILE.INC`, `QUOTIENT`
- **R**: `RANDARRAY`, `RANK`, `RANK.AVG`, `RECEIVED`, `REDUCE`, `REGEXEXTRACT`, `REGEXREPLACE`,
  `REGEXTEST`, `REGISTER.ID`, `REPLACEB`, `RIGHTB`, `ROMAN`, `RRI`, `RSQ`, `RTD`
- **S**: `SCAN`, `SEARCHB`, `SERIESSUM`, `SHEET`, `SHEETS`, `SKEW`, `SKEW.P`, `SLN`, `SLOPE`,
  `SQRTPI`, `STANDARDIZE`, `STDEV`, `STDEVA`, `STDEVP`, `STDEVPA`, `STEYX`, `STOCKHISTORY`, `SUMSQ`,
  `SUMX2MY2`, `SUMX2PY2`, `SUMXMY2`, `SYD`
- **T**: `T`, `T.DIST`, `T.DIST.2T`, `T.DIST.RT`, `T.INV`, `T.INV.2T`, `T.TEST`, `TAKE`, `TBILLEQ`,
  `TBILLPRICE`, `TBILLYIELD`, `TDIST`, `TEXTAFTER`, `TEXTBEFORE`, `TEXTSPLIT`, `TIMEVALUE`, `TINV`,
  `TOCOL`, `TOROW`, `TRANSLATE`, `TREND`, `TRIMMEAN`, `TRIMRANGE`, `TRUE`, `TTEST`, `TYPE`
- **U**: `UNICHAR`, `UNICODE`
- **V**: `VALUETOTEXT`, `VAR`, `VARA`, `VARP`, `VARPA`, `VDB`, `VSTACK`
- **W**: `WEBSERVICE`, `WEEKNUM`, `WEIBULL`, `WEIBULL.DIST`, `WORKDAY.INTL`, `WRAPCOLS`, `WRAPROWS`
- **Y**: `YIELD`, `YIELDDISC`, `YIELDMAT`
- **Z**: `Z.TEST`, `ZTEST`

## Order of work

1. **The P1 Ready functions** — done, ticket 01. Their `uncertain` cases go to the next Windows run.
2. **The P1 Observe functions** — observed on October 3 and admitted in tickets 08–09 within
   explicit domains: the criteria of the `SUMIF` family and legacy lookups.
3. **The P1 Decide functions, to the user.** Done: `TEXT`, ticket 02 (ADR-0120), and `TODAY`,
   ticket 03 (ADR-0121, ADR-0122).
4. **The P2 Ready functions** — done, ticket 04.
5. **The P3 Ready functions** — done, ticket 05. No function is Ready any more.
6. **`LOG` and the trigonometric functions** — done, ticket 07. Their `uncertain` cases go to the
   next Windows run. The seventeenth Windows run answered them (2026-10-11).
7. **The October 3 Windows observation run is complete:** all 24 Observe functions and `LET`,
   `RAND`, `RANDBETWEEN`. Tickets 08–12 admit 21 functions within explicitly documented argument
   domains. Observation alone does not mark a function Supported.
8. **Financial iteration remains open**, ticket 13: the measured Excel results are available,
   but the tested candidate solvers do not meet the admission rule.
9. **LET and random functions need decisions**, tickets 14–15. Their observation is complete.
   *(Decided on 2026-10-10: item 11.)*
10. **Existing Supported functions' `uncertain` cases remain a separate Oracle backlog.** The
    October 3 run did not ask them, including LOG and trigonometry's remaining cases.
11. **The last Decide rows, decided with the user on 2026-10-10.** Ready: `RAND` and `RANDBETWEEN`
    (ADR-0180, ticket 15), `LET` (ADR-0181, ticket 14), `SUBTOTAL` and `AGGREGATE` (ticket 16),
    `ERROR.TYPE` (ticket 17) and `INDIRECT` (ticket 18). Blocked: `IRR`, `XIRR` and `RATE`, which
    are not pursued for now (ticket 13). No row is Decide any more.
12. **Tickets 14 to 18 built** (2026-10-11): `ERROR.TYPE`, `SUBTOTAL`, `AGGREGATE`, `INDIRECT`, `LET`,
    `RAND` and `RANDBETWEEN` are Supported. Their `uncertain` cases, and the questions their tickets
    name, go to the next Windows run. No row is Ready any more.
13. **Excel's functions not in the queue are listed** (2026-10-11, ADR-0181's note of that date): a
    `LET` name is refused if it is the name of any of Excel's functions, as this file lists them, so
    the list beside the rows is what the engine refuses, kept equal to it by `FunctionCatalogueTests`.
    *(2026-10-11, the seventeenth run: Excel's keyboard took a function's name as a `LET` name, so the
    list is no longer what the engine refuses; see the list's paragraph.)*
14. **The area pass of 2026-10-12 asked the financial functions' open cases** (ADR-0202's note of
    that date, `verification/2026-10-12-windows-area/`). `PV` and `FV` read any `type`, and answer an
    `nper` that is not a whole number, or is negative, where every evaluation Excel's doubles leave gives
    the same double; where those part (half a period, a negative fraction, a rate of -1, and `FV` with a
    present value and a payment) and at a rate below -1 with a fractional `nper` they are refused until
    Excel is asked. `XNPV` is refused where the six evaluations it computes part (whole years, the days
    over 365 or times 1/365) and at a rate of -1 or below; 72 evaluations give its two observed doubles
    and part at most other inputs, which is the user's question. `PMT` reads `type` as they do; a non-whole or negative `nper` is
    refused, and five of its payments differ from Excel's by one to three units in the last place,
    which no evaluation searched explains, so whether it is corrected or refused is the user's
    question. `NPV` is Blocked: no evaluation searched gives every double Excel gave for it. Ticket 13
    holds both.

The retained evidence is `verification/2026-10-03-windows-functions/`. New corpus cases preserve
its IDs, typed fixtures and exact numeric expectations. A deliberate refusal keeps Excel's
observed result in `excelExpect` and names ADR-0047 in `engineDiffersByDecision`. The engine's
README lists the admitted domains; broadening one requires evidence, not a relaxed assertion.
