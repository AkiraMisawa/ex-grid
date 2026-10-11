# ExSheet's function catalogue

Status: done — but for [ticket 13](issues/13-financial-iteration.md) (`needs-info`): `IRR`, `XIRR` and
`RATE` are Blocked, since no algorithm reproduces Excel's, and are not pursued for now. Tickets 14 to 18,
decided with the user on 2026-10-10 (item 11 below), are built and done (item 12); their questions, with
every other ticket's `uncertain` cases, are corpus cases the sign-off's Windows run puts to Excel, but
for two of ticket 15's that the corpus cannot ask (what an undo does to a draw, and whether a rename draws
again), which that run asks by hand.
*(Set on 2026-10-11.)*

The functions ExSheet's engine supports, and the queue of those it may support next, each with its
status and a priority. **ADR-0047 decides whether a function is admitted; this file only orders the
queue.** A function is admitted when it matches Excel, edge cases included, or gives an Error Value
for the argument values where it cannot. It never gives a different answer. A priority does not
change that rule, and a function listed here is not a promise.

`tests/ExSheet.Engine.Tests/FunctionCatalogueTests.cs` reads this file: the rows marked
**Supported** must be exactly `DeclaredFunction.All`. Admitting a function without changing its row
here fails the build, and so does changing a row without admitting the function. Every name the
file lists, its rows and its list of [Excel's functions not in the queue](#excels-functions-not-in-the-queue),
must be exactly the names the engine refuses as a `LET` name for being a function's (ADR-0181).

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
| `LARGE` | Supported | — | |
| `SMALL` | Supported | — | |
| `RANK.EQ` | Supported | — | |
| `STDEV.S` | Supported | — | |
| `STDEV.P` | Supported | — | |
| `VAR.S` | Supported | — | |
| `VAR.P` | Supported | — | |
| `SUBTOTAL` | Supported | — | Admitted while a Sheet cannot hide a row, behind a test that fails when one can; a nested call other than a whole-Formula `SUBTOTAL` is `#VALUE!` until observed (ADR-0047, 2026-10-10). Ticket 16 |
| `AGGREGATE` | Supported | — | As `SUBTOTAL`; options 0 to 3 pass over a whole-Formula `SUBTOTAL` or `AGGREGATE`, and a function number for a function the engine does not support (13, 16 to 19) is `#VALUE!`. Ticket 16 |

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
| `ERROR.TYPE` | Supported | — | `#GETTING_DATA` and `#CIRC!` are given back, never numbered; an Error Value outside Microsoft's table is `#VALUE!` until Excel is asked (ADR-0047, 2026-10-10). Ticket 17 |

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
| `INDIRECT` | Supported | — | Volatile, as `OFFSET`; A1-style text naming a place on the same Sheet, not rewritten by an insertion; R1C1, structured and `A1#` text are `#VALUE!`, and text that names no place is `#REF!` (ADR-0124, 2026-10-10). Ticket 18 |

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
| `IRR` | Blocked | P3 | Observation complete; candidate solvers do not reproduce Excel's root, stopping result and failures to 15 significant digits. Not pursued for now (ADR-0047, 2026-10-10). Ticket 13 |
| `XIRR` | Blocked | P3 | Observation complete; candidate solvers do not reproduce Excel's root, stopping result and failures to 15 significant digits. Not pursued for now (ADR-0047, 2026-10-10). Ticket 13 |
| `RATE` | Blocked | P3 | Observation complete; candidate solvers do not reproduce Excel's root, stopping result and failures to 15 significant digits. Not pursued for now (ADR-0047, 2026-10-10). Ticket 13 |

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
| `LET` | Supported | — | Names bound inside one Formula, as Excel's keyboard accepts them; the name of any of Excel's functions this file lists, declared or not, `TRUE`, `FALSE` and R1C1 shapes are refused on entry until observed (ADR-0181). Ticket 14 |

### Random

| Function | Status | Priority | Notes |
|---|---|---|---|
| `RAND` | Supported | — | Draws from the Sheet Seed, and draws again at each change, never on the clock (ADR-0180). Ticket 15 |
| `RANDBETWEEN` | Supported | — | As `RAND`; a fractional bound and a bound past 2^53 are `#VALUE!` (ADR-0180). Ticket 15 |

## Excel's functions not in the queue

The rest of Excel's worksheet functions, as Microsoft documents them. None of them is queued: each
is `#NAME?` (ADR-0047), and none has a status or a priority until a row above catalogues it. They are
listed because a `LET` name is refused if it is the name of any of Excel's functions, as the rows
above and this list name them, whether or not the engine declares it (ADR-0181's note of
2026-10-11): a Sheet saved with `LET(NPER, …)` would otherwise stop opening on the day `NPER` is
declared.

`FunctionCatalogueTests` reads this list with the rows above, and together they must be exactly the
names the engine refuses as a function's (`ExcelFunctionNames`). Cataloguing a function moves its name
from here to a row; a function Excel adds is listed here, and in the engine, in the same change.

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
   next Windows run.
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

The retained evidence is `verification/2026-10-03-windows-functions/`. New corpus cases preserve
its IDs, typed fixtures and exact numeric expectations. A deliberate refusal keeps Excel's
observed result in `excelExpect` and names ADR-0047 in `engineDiffersByDecision`. The engine's
README lists the admitted domains; broadening one requires evidence, not a relaxed assertion.
