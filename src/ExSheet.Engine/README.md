# ExSheet.Engine

The formula engine of ExSheet, with no UI and no dependency. It holds a **Sheet** — Excel's
extent, 1,048,576 rows by 16,384 columns, held sparsely — and computes the **Value** of every
cell from its **Entry**:

- Formulas in Excel's syntax, written the same way in every culture (`,` between arguments, `.`
  as the decimal separator, English function names)
- a declared set of functions, each giving Excel's result; a function outside the set is `#NAME?`,
  never an approximation
- recalculation of only what a change reaches, publishing Values only from a completed
  recalculation; a circular reference is `#CIRC!` in every cell of the cycle and every cell that
  depends on it

The **Sheet Document** is the Sheet's serialisable form (version 9, which also reads versions 1
to 8), with the Cell Formats recorded on its columns, rows and cells — Number Format, Alignment,
Font, Fill and Borders — and the widths recorded on its columns. It holds Entries and never Values, so
anyone who wants a saved Sheet's numbers runs this engine — on a server as in the browser, with
the same result.

> **ExSheet ships with ExGrid, at the same version;** upgrade them together. While the version is
> `0.x`, the API may change from one version to the next; each version's
> [Release Notes](https://akiramisawa.github.io/ex-grid/release-notes) say what changed.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0` and has no dependencies.

## Install

```sh
dotnet add package ExSheet.Engine
```

Add `--prerelease` for a prerelease, a version with a suffix such as `-rc.1`.

## Computing a Sheet

```csharp
using System.Globalization;
using ExSheet.Engine;

var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
sheet.Enter(CellAddress.Parse("A1"), "2");
sheet.Enter(CellAddress.Parse("B1"), "3");
var change = sheet.Enter(CellAddress.Parse("C1"), "=A1+B1");

sheet.GetValue(CellAddress.Parse("C1"));   // 5
change.ValueChanges;                        // C1
```

`Enter` reads text as a user typed it: text beginning with `=` is a Formula, and anything else is
a constant read under the Sheet's culture and recorded already parsed. Typing into a General cell
can give it a format, as in Excel: a date or a percentage typed as one, and a Formula of simple
arithmetic (`+`, `-`, single-cell References, parentheses) takes the format of the first
formatted cell it reads, so `=A1+7` over a date shows a date; `DATE` gives the short date
format. It happens once, at entry. A Formula keeps the whitespace it was typed with where Excel
keeps it — before a token, but not at the end nor before a `,` — and a tab between tokens is refused, as Excel refuses it. Its tokens are written in
Excel's spelling (`= sum( a1 , 2 ) ` is kept as `= SUM( A1, 2 )`), and rewriting its References
changes only the Reference tokens; one rewritten to `#REF!` takes the whitespace before it along
and keeps its qualifier (`Sheet1!#REF!`). References are written as Excel writes them: a range
spanning every row as whole columns (`A1:A1048576` is `A:A`), one spanning every column as whole
rows (`A:XFD` is `$1:$1048576`), and a structured reference in single brackets
(`Positions[[Market Value]]` is `Positions[Market Value]`).

A Sheet has a name, `Sheet1` unless it is given one (`new Sheet(culture, "Risk")`), and
`SheetEdit.Rename` changes it as Excel does, rewriting every Reference qualified with the old
name. A Reference qualified with the Sheet's own name (`Sheet1!A1`, `'My Sheet'!A1`) reads its
cells; any other qualifier is `#REF!`. `Sheet.IsValidName` applies Excel's rules: 1 to 31
characters, none of `: \ / ? * [ ]`, not beginning or ending with `'`, and not `History`.

## What a cell shows in its column

`Sheet.GetDisplay(address, width)` is what the cell shows in a column `width` characters wide —
Excel's unit, the number of digits of the font that fit between the cell's paddings, with
`Sheet.DefaultColumnWidth` (8.43) as Excel's default. Every character of a number's text is
charged one digit width, so a column holds `⌊width⌋` characters. A component converts from its
pixel width as `(columnPx − 2 × paddingPx) / digitWidthPx`.

```csharp
sheet.Enter(CellAddress.Parse("A1"), "=1/3");
sheet.GetDisplay(CellAddress.Parse("A1"), Sheet.DefaultColumnWidth).Text;   // 0.333333
sheet.GetDisplay(CellAddress.Parse("A1")).Text;                             // 0.333333333333333
```

- **General fits the column**, as Excel's does: decimals are rounded to the width, and the number
  is written in scientific notation where its integer part does not fit, where it has twelve or
  more digits, or where that shows it more closely. It never takes more than eleven characters
  besides a minus sign. Where not even `1E+08` fits, the cell `CannotShow` (`####`).
- **Any other format is never shortened.** A formatted number or a date longer than the width
  cannot show, which is Excel's `####` (ADR-0016).
- `GetDisplay(address)` is the text at no width: General in full, at fifteen significant digits,
  for an accessible name or a copy.

`Sheet.GetWidthOnEntry(address)` is the width, in characters, that the number typed into a cell
needs, for widening a column at its default width or one widened by entry, as Excel does: every integer digit
without scientific notation (decimals are rounded instead), the scientific form of twelve or more
digits, or the whole text of a date or another formatted number. It is `null` for text, a
Formula and anything else that never widens a column. Which of these rules Microsoft documents,
and which are still to be observed in Excel, the case corpus says per case. It is read under the
cell's Number Format as it is now, so a component asks it again after setting a Number Format, for
each cell of `Sheet.EntryAddressesIn(range)`: Excel's formatting keys widen a column as an entry
does (ADR-0071).

## Functions

`DeclaredFunction.All` lists the declared set, with each function's arguments as Excel documents
them:

| Function | Arguments |
|---|---|
| `SUM`, `AVERAGE`, `MIN`, `MAX`, `PRODUCT`, `MEDIAN` | `number1, [number2], ...` |
| `COUNT`, `COUNTA` | `value1, [value2], ...` |
| `SUMIF` / `AVERAGEIF` | `range, criteria, [sum_range]` / `range, criteria, [average_range]` |
| `SUMIFS` / `AVERAGEIFS` / `MAXIFS` / `MINIFS` | result range, then `criteria_range1, criteria1, ...` |
| `COUNTIF` / `COUNTIFS` | `range, criteria` / `criteria_range1, criteria1, ...` |
| `COUNTBLANK` | `range` |
| `VLOOKUP` / `HLOOKUP` | `lookup_value, table_array, col_index_num, [range_lookup]` / `lookup_value, table_array, row_index_num, [range_lookup]` |
| `MATCH` | `lookup_value, lookup_array, [match_type]` |
| `MROUND` | `number, multiple` (whole-number multiples) |
| `CEILING.MATH`, `FLOOR.MATH` | `number, [significance], [mode]` |
| `YEARFRAC` / `DATEDIF` | `start_date, end_date, [basis]` / `start_date, end_date, unit` |
| `UPPER`, `LOWER`, `PROPER` | `text` |
| `SEARCH` | `find_text, within_text, [start_num]` |
| `LARGE`, `SMALL` | `array, k` |
| `RANK.EQ` | `number, ref, [order]` |
| `STDEV.S`, `STDEV.P`, `VAR.S`, `VAR.P` | `number1, [number2], ...` |
| `SUBTOTAL` | `function_num, ref1, [ref2], ...` — 1 to 11, and 101 to 111 |
| `AGGREGATE` | `function_num, options, ref1, [ref2], ...`, or `function_num, options, array, k` for `LARGE` and `SMALL` |
| `IF` | `logical_test, value_if_true, [value_if_false]` |
| `IFS` | `logical_test1, value_if_true1, ...` |
| `SWITCH` | `expression, value1, result1, [default_or_value2], [result2], ...` |
| `AND`, `OR`, `XOR` | `logical1, [logical2], ...` — typed text that spells neither TRUE nor FALSE is passed over, as text in a range is |
| `NOT` | `logical` |
| `IFERROR` | `value, value_if_error` |
| `IFNA` | `value, value_if_na` |
| `ISERROR`, `ISBLANK`, `ISNUMBER`, `ISTEXT`, `ISNA` | `value` |
| `ERROR.TYPE` | `error_val` — Microsoft's number, 1 to 7, and Excel's, 9 to 14, for its newer Error Values; `#GETTING_DATA` and `#CIRC!` given back |
| `NA`, `PI` | (none) |
| `ROUND`, `ROUNDUP`, `ROUNDDOWN` | `number, num_digits` |
| `TRUNC` | `number, [num_digits]` |
| `ABS`, `INT`, `SQRT`, `SIGN`, `EXP`, `LN`, `LOG10` | `number` |
| `LOG` | `number, [base]` |
| `SIN`, `COS`, `TAN`, `COT`, `CSC`, `SEC`, `ASIN`, `ACOS`, `ATAN`, `ACOT`, `SINH`, `COSH`, `TANH`, `COTH`, `CSCH`, `SECH`, `ASINH`, `ACOSH`, `ATANH`, `ACOTH` | `number` |
| `ATAN2` | `x_num, y_num` |
| `DEGREES`, `RADIANS` | `angle` |
| `MOD` | `number, divisor` |
| `POWER` | `number, power` |
| `DATE` | `year, month, day` |
| `TODAY` | (none) — the Sheet Day, `#GETTING_DATA` until it is set (`Sheet.SetToday`, ADR-0121) |
| `NOW` | (none) — the moment `Sheet.NowSource` gives, read once per recalculation; volatile (ADR-0124) |
| `RAND` | (none) — drawn from the Sheet Seed (`Sheet.SetSeed`), `#GETTING_DATA` until it is set; volatile (ADR-0180) |
| `RANDBETWEEN` | `bottom, top` — as `RAND`, a whole number with both bounds drawn |
| `YEAR`, `MONTH`, `DAY`, `HOUR`, `MINUTE`, `SECOND` | `serial_number` |
| `TIME` | `hour, minute, second` |
| `WEEKDAY` | `serial_number, [return_type]` |
| `DAYS` | `end_date, start_date` |
| `EOMONTH`, `EDATE` | `start_date, months` |
| `NETWORKDAYS` | `start_date, end_date, [holidays]` |
| `WORKDAY` | `start_date, days, [holidays]` |
| `LEFT`, `RIGHT` | `text, [num_chars]` |
| `MID` | `text, start_num, num_chars` |
| `LEN`, `TRIM` | `text` |
| `CONCAT`, `CONCATENATE` | `text1, [text2], ...` |
| `TEXTJOIN` | `delimiter, ignore_empty, text1, [text2], ...` |
| `SUBSTITUTE` | `text, old_text, new_text, [instance_num]` |
| `REPLACE` | `old_text, start_num, num_chars, new_text` |
| `FIND` | `find_text, within_text, [start_num]` |
| `REPT` | `text, number_times` |
| `EXACT` | `text1, text2` |
| `NUMBERVALUE` | `text, [decimal_separator], [group_separator]` |
| `TEXT` | `value, format_text` |
| `VALUE` | `text` — read under the Sheet's culture (ADR-0124) |
| `INDEX` | `array, row_num, [column_num]` |
| `CHOOSE` | `index_num, value1, [value2], ...` |
| `OFFSET` | `reference, rows, cols, [height], [width]` — volatile (ADR-0124) |
| `INDIRECT` | `ref_text, [a1]` — text naming a place on the same Sheet, A1 or R1C1 (offsets in brackets, whole rows and columns, `R1C1#` included), a Linked Table's column, or `A1#`; volatile (ADR-0124) |
| `ROW`, `COLUMN` | `[reference]` |
| `ROWS`, `COLUMNS` | `array` |
| `XLOOKUP` | `lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode]` |
| `XMATCH` | `lookup_value, lookup_array, [match_mode], [search_mode]` |
| `PMT` | `rate, nper, pv, [fv], [type]` |
| `PV`, `FV` | `rate, nper, pmt, [fv or pv], [type]` |
| `XNPV` | `rate, values, dates` |
| `FILTER` | `array, include, [if_empty]` |
| `UNIQUE` | `array, [by_col], [exactly_once]` |
| `SORT` | `array, [sort_index], [sort_order], [by_col]` |
| `SORTBY` | `array, by_array1, [sort_order1], ...` |
| `SEQUENCE` | `rows, [columns], [start], [step]` |
| `TRANSPOSE` | `array` |
| `LET` | `name1, name_value1, calculation_or_name2, [name_value2, calculation_or_name3...]` |
| `SUMPRODUCT` | `array1, [array2], ...` |

A Formula whose result is an array of more than one Value **spills** (ADR-0125): its cell, the
Anchor, shows the first Value, and the cells below and to the right show the rest, holding no
Entry of their own. `Sheet.SpilledFrom(address)` names the Anchor of a spilled cell. An Entry or
another spill in the way, or the Sheet's edge, makes the Anchor `#SPILL!`, and the spill is laid
out again when the obstacle is cleared; of two spills that would overlap, the Anchor first in
address order spills. `A1#` reads the spill of the Formula in A1, and is `#REF!` where A1 does not
spill. Operators work element by element over ranges, and a function that reduces (`SUM`,
`SUMPRODUCT`) takes an array whole. A blank in a spilled array shows 0. Among `SORT`'s and
`SORTBY`'s keys a blank sorts after numbers, text and booleans, in either order, blanks keeping
their order: Excel's `SORT` was seen to sort it so after numbers and text in either order and after
a boolean ascending, keeping two blank keys in their order in a descending sort, and its `SORTBY`
after a number in an ascending key; the rest is the engine's, by generalisation, until Excel is
asked. An Error Value key sorts after numbers, text and booleans ascending and before them descending,
and before a blank, in `SORT` and `SORTBY` alike, as Windows run 18a read Excel (SORT-026, 028 to 031,
SORTBY-010); two different ones order as `ERROR.TYPE` numbers them where the order of their texts
agrees, `#DIV/0!` before `#N/A` (SORT-032), and make the Formula `#VALUE!` elsewhere and in a
descending sort. A `sort_index` that is not a whole number is truncated (SORT-042). In `UNIQUE` a blank is one of a kind only
with another blank, never with 0. Read inside a Formula, the blank `UNIQUE`, `SORT`, `SORTBY`,
`FILTER` and `TRANSPOSE` keep is read as the empty value a blank cell holds: `COUNTA` counts it in
the array, `INDEX` takes it out as a blank, and an operator, or most functions that read one Value
(`LEN`, `ISTEXT`, `MATCH`'s lookup value), read it as a blank cell's; the rest, `XLOOKUP`'s lookup
value and `ROUND` among them, make the Formula `#VALUE!`. Excel was seen to give answers that fit
this in three cases: `ISBLANK` found the blank `INDEX` took out of `SORT`'s blank (SORT-022), `=`
found the one it took out of `UNIQUE`'s equal to empty text (UNIQUE-018), and `COUNTA` counted it in
both arrays (SORT-021, UNIQUE-017); the rest is the engine's, carried from those until Excel is
asked. The aggregates pass over it as over a blank cell, `SUM`, `AVERAGE`, `MIN` and `COUNT` as Excel
was seen to (SORT-034, 037, FILTER-014, TRANSPOSE-007, UNIQUE-021, 022) and `MAX`, `PRODUCT`,
`MEDIAN` and the spreads by generalisation; `COUNTA` counts it given as one Value too (SORT-035,
UNIQUE-023); `IF` hands it on as it is (SORT-039); and `SORT` sorts it last as a blank key
(SORT-038). `SUMPRODUCT` and `CONCAT` add nothing for it, and `VLOOKUP`, `HLOOKUP` and `XLOOKUP` take an array that holds it where they never read
it, each because every reading gives the one answer. A function that would read it some other way no
run has settled makes the Formula `#VALUE!`.

Which functions come next, each with its status and priority, is catalogued in
[`docs/specs/exsheet-functions/spec.md`](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/specs/exsheet-functions/spec.md).

`DeclaredFunction.ValuesOf(index)` gives the values an argument takes from a fixed list, in Excel's
order and with Excel's texts, which completion lists there: `XLOOKUP`'s `match_mode` (`0 - Exact
match`, `-1 - Exact match or next smaller item`, …) and `search_mode` (`1 - Search first-to-last`, …).
The newly admitted functions also offer `VLOOKUP`/`HLOOKUP`'s `range_lookup`, `MATCH`'s
`match_type`, and `YEARFRAC`'s `basis`. Their values and meanings follow Microsoft's documentation;
tickets 09 and 11 retain Windows popup confirmation as a follow-up.

Where the engine cannot give Excel's answer, it gives an Error Value and never a different
answer. Such a `#VALUE!` is a refusal, and no Formula catches it: not `IFERROR`, `ISERROR`, `COUNT` or
any other function around it in the same Formula, and not a Formula in another cell that reads it —
directly, through a range, a name, `INDIRECT`, `OFFSET`, a Spill Range or a Linked Table — which is
refused in turn, as one that reads a cycle is `#CIRC!` (ADR-0047, 2026-10-12, decided with the user).
A cell's refused Value is `#VALUE!` in every other way, and `Value.IsRefused` says it is one;
`Entry.FromValue` refuses it. Excel's own `#VALUE!`, `=1+"a"`'s, is caught as Excel's is. A call of a
function Excel has that ExSheet does not declare (`ExcelFunctionNames`), and a declared Linked Table's
bare name typed, are refused too and show `#NAME?` (2026-10-13, decided with the user); a cell that
reads one is refused and shows `#VALUE!`, and a name Excel does not have is Excel's own `#NAME?`. A
refused Anchor whose array's shape its text settles spills refusals over that shape; where the shape
could depend on a Value, nothing spills (ADR-0125, 2026-10-13). The refusals:

- **Functions are not lifted over arrays yet** (ADR-0125). An array where a function wants one
  Value — `ROUND(A1:A3,0)`, `IF(A1:A3>1,…)` — makes the Formula `#VALUE!`, where Excel 365 applies
  the function to each Value and spills. The refusal is the Formula's: `IFERROR`, `ISERROR`,
  `COUNTA` and the rest do not turn it into an answer, given the range or around a call that was
  given it, so `=IFERROR(LEN(A1:A3),0)` is `#VALUE!`. The `@` operator is refused on entry.
- **An array larger than the engine holds**, more than 2^24 Values, is `#NUM!` (ADR-0125).
- **`XLOOKUP`'s and `XMATCH`'s binary search** (`search_mode` 2 and −2) answers only over a lookup array sorted
  as the mode says — one kind of value, no blanks, text of ASCII letters, digits and spaces; it is
  `#VALUE!` otherwise. Over duplicate keys it returns the one Excel was observed to: an equal key
  is the first of its run ascending and the last descending.
- **`#CIRC!`** is shown by every cell of a circular reference and every Formula that reads one,
  where Excel shows 0. `IFERROR` and `IFNA` do not catch it.
- **`#GETTING_DATA`**, a Linked Table's data on its way, is not an error to `IFERROR` and
  `ISERROR`, where Excel's are: a fallback never stands in for data that has not arrived.
  `ISBLANK` and `ISNUMBER` give it back too, rather than FALSE, and `ERROR.TYPE` gives it and
  `#CIRC!` back rather than a number (Microsoft's table numbers `#GETTING_DATA` 8).
- **`TEXT` reads its code in the invariant spelling** under every culture (`"yyyy"`, never German
  Excel's `"JJJJ"`), as a cell's number format records it, and shows the Value as a cell in that
  format would under the Sheet's culture (ADR-0120). `General` is fitted to 11 characters, as a
  cell's General is fitted to a wide column, and a minus sign is not one of them
  (`TEXT(-1/3,"General")` is `-0.333333333`); a lone `@` shows a number as that General does; and an
  empty code shows a number as empty text, as Excel's do. A code outside the cell formats' subset is
  `#VALUE!`, a fraction or a condition among them, though Excel answers both.
- **Asked of Excel before they are answered**, each refused until the next Windows run says what
  Excel gives: `EDATE` from serial 0 to January 1900, March 1900 or January 10000, where the readings
  that give Excel's other answers part, `EOMONTH` with a boolean typed as its start, and `EDATE` with
  one typed as its months (`#VALUE!`); `NETWORKDAYS` and `WORKDAY` with a boolean typed as a date
  (`#VALUE!`); `MOD` where the evaluations at 64 bits that give every answer Excel gave part, as at
  `MOD(-1E-17,1)` and `MOD(-0.084474,2270)` (`#VALUE!`; MOD-033 to 036); `PV` at a rate of -1 with an
  `nper` above 0; `PMT` at a rate of -1 with periods, below -1 with a whole `nper` or a fraction whose
  reciprocal is odd, where a quantity the evaluations Excel's payments leave compute passes a double's
  range or falls below its normal numbers, at a growth below 2^-52, with no periods at the start, and
  where one of them magnifies its own roundings more than 2^6 times; `PMT`, `PV` and `FV` with an
  `nper` past 2^53 at a rate other than 0; and `XNPV` at a rate of 0, where its last addition nearly
  cancels, and where an Error Value in its rate or ranges fails beside another check that gives
  another answer (`#VALUE!`); `SORT` and `SORTBY` with two different Error Value keys whose readings
  part, or in a descending sort (`#VALUE!`); `DATEDIF`, `YEARFRAC`, `HOUR`, `MINUTE` and `SECOND` with a serial between -1 and 0
  (`#VALUE!`; DATEDIF-048, YEARFRAC-058, HOUR-005, MINUTE-003, SECOND-003); a blank `UNIQUE`, `SORT`, `SORTBY`, `FILTER` or `TRANSPOSE` keeps, read
  by a function whose readings of it part, such as `MATCH`'s lookup array or `LEN` (`#VALUE!`). Each of
  these refusals is the Formula's, and every Formula's that reads its cell: no Formula catches it.
- **Answered as the seventeenth Windows run read Excel.** `MOD` answers a quotient of 2^27 or more, and
  is `#NUM!` from 1,125,900,000,000, the limit a Microsoft 365 user reported, as the area pass of
  2026-10-12 read Excel on both sides of it (MOD-010 to 016). `LARGE`, and `AGGREGATE`'s 14, round a
  `k` that is not whole up: `LARGE(A1:B5,1.5)` is the
  2nd largest. `k` is read at 15 significant digits. `RANK.EQ`'s `ref`, `SUBTOTAL`'s references and
  `AGGREGATE`'s fifth and later arguments refuse on entry what can never be a Reference — a constant,
  an operator's result, a call such as `SEQUENCE(3)` — as Excel does.
- **Answered as the area passes of 2026-10-12 read Excel.** `SMALL`, and `AGGREGATE`'s 15, truncate a
  `k` that is not whole: `SMALL(A1:A9,1.5)` is the smallest. A `k` below 1 or above the count of the
  numbers is `#NUM!` for `LARGE` and `SMALL` alike, whole or not, before it is rounded:
  `SMALL(A1:A9,9.5)` over nine numbers is `#NUM!`. `AGGREGATE`'s array form takes text or a boolean
  given directly as an array holding no number: `AGGREGATE(14,6,"5",1)` is `#NUM!`. `MOD`'s remainder is
  exact, as Excel's is: `MOD(123456789012,0.7)` is 0.5000078322611261 and `MOD(1,0.1)`
  0.09999999999999995, where `number - divisor*INT(number/divisor)` in doubles gives 0.5000152587890625
  and 0. Where the divisor is added to a remainder of the other sign, the sum is held at 64 bits before
  it is stored as a double, which gives Excel's `MOD(-0.087626,6630)`, 6629.9123739999995, and a sum
  stored as the divisor itself is 0, as Excel's `MOD(-1E-20,1)` is. Where the evaluations at 64 bits
  that give every answer Excel gave still part, `MOD` is `#VALUE!` (above). The same evaluations with the
  sum held at a width no hardware holds (54 to 63 bits rounded, 54 to 65 truncated) give those answers
  too; the engine does not model them, and answers as 64 bits do where they would give another last
  digit, about 3 inputs of up to five digits in 100, a judgment Excel has yet to settle (MOD-039, 040).
- **`PV`, `FV` and `XNPV` give every double and Error Value Excel was observed to give, to the bit**
  (ADR-0047, the seventeenth run and the area passes of 2026-10-12). `PV` and `FV`
  evaluate Microsoft's identity in doubles, the payment times (1+rate·type)·((g−1)/rate), with g the
  financial power: squaring to a whole `nper`, 1 over the power to a negative one, the square root at
  0.5, and e^(nper ln(1+rate)) otherwise, the exponential and the logarithm correctly rounded
  (`CorrectlyRounded`, the same on every platform; Excel's exponential is the correctly rounded one at
  XNPV-013, and no observed double tells logarithms apart); a negative 1+rate has an odd root where
  1/nper is odd and is `#NUM!` otherwise, and 1+rate of 0 is `#NUM!` to the power 0 and `#DIV/0!` to a
  negative one. `XNPV` divides each value by that power of its days over 365 and sums from the first;
  it checks the rate before it evaluates and is `#NUM!` below 0, Excel's answer at the rates asked
  (-0.5 to -2) and the engine's at others, refuses any cell of its values or dates that is not a
  number (text, a boolean or a blank), whatever else is among them, and refuses an Error Value in its
  rate or ranges beside another check that would give another answer. `type` is a truth value: any number other than 0, 0.5
  included, is payment at the start. **`PMT` is kept under ADR-0047's exception for `PMT` alone**: where
  it answers, the payment at the end is the exact payment rounded once, held between two bounds that
  decide its double (`Enclosure`, interval arithmetic over exact dyadic numbers, (1+rate)^nper - 1
  raised from the rate itself), and the payment at the start the exact payment at the start rounded
  once, held between bounds as well (2026-10-13, decided with the user). Excel's payment differs from
  ExSheet's at 23 of the 30 observed, by 1 to 11 units in the last place, at PMT-013 and PMT-026 in the
  15th significant digit, and no evaluation searched gives every one (a Known limitation of 0.1.0). Where the
  evaluations Excel's payments leave part in kind, or beyond the last digits, `PMT` refuses
  (`ExactPayment`, `Functions.Financial.cs`).
- **`SIN`, `COS` and `TAN` reduce an angle by Excel's pi** (ADR-0047, 2026-10-12): pi rounded to the
  x87's 64 bits, so `SIN(PI())` is 1.22514845490862E-16, as Excel's is, and not the sine of the double
  `PI()` gives. Each is the double nearest the exact value; `COT`, `CSC` and `SEC` are 1 over them.
  From 2^27 the six are `#NUM!`, as Excel's; `COTH`, `CSCH` and `SECH` answer past it, as Excel's do.
- **`RAND` and `RANDBETWEEN` draw from the Sheet Seed** (ADR-0180). Excel names no generator and no
  seed, so its numbers cannot be matched; what is Excel's is its contract — a draw of `RAND` in
  [0,1), evenly spread; `RANDBETWEEN`'s whole numbers, both bounds drawn; two calls two draws; a
  `LET` binding one draw. Each draw is a function of `Sheet.Seed`, the cell, the call's place in its
  Formula and `Sheet.Changes`, the count of changes since the Sheet was made or opened, so the same
  Entries, seed and count give the same draws whatever order cells are computed in. An edit, an
  insertion or a deletion, a rename of the Sheet, the undo or redo of one, a Linked Table's snapshot
  whatever its rows, and a Linked Table's declaration that declares it or replaces it each count once
  and draw again; a recalculation by the clock, a format, a rename to the Sheet's own name and a new
  seed draw nothing. Without a seed,
  every Formula that draws is `#GETTING_DATA`. The engine never chooses a seed of its own:
  `Sheet.SetSeed(seed)` gives one, and `SetSeed(seed, changes)` the count as well, so a Sheet opened
  from a document shows another Sheet's draws (SH-17). Neither is part of a Sheet Document. Unlike
  Excel, nothing draws without a change, and there is no manual recalculation. An undo, a redo and a
  rename draw again, as Excel's were seen to (ADR-0180, 2026-10-12).
  `RANDBETWEEN` refuses a fractional bound and a bound past 2^53 in magnitude with `#VALUE!`, and
  otherwise answers as Excel was observed to: a bottom above the top is `#NUM!`, a boolean and text
  that is not a number are `#VALUE!`, a numeric string is its number and a blank is 0.
- **`LET` takes the names Excel's keyboard accepts** (ADR-0181). A name is bound for the values
  after it and its own calculation, sequentially and lexically, and compared without regard to
  case; a name read before its binding, or inside its own value, is `#NAME?`. A name starts with a
  letter of any script or `_` and goes on with letters, digits and `_`, at most 249 characters, and
  a `LET` binds at most 126. The name of a function is a name, whether the engine declares it or not
  (`SUM`, `IRR`, `N`), and so is an R1C1 shape that does not read as an A1 Reference (`R1C1`, `RC`),
  as Excel's keyboard took them in the seventeenth Windows run; a name followed by `(` is still the
  function's call, so `LET(SUM,2,SUM(SUM,1))` is 3. A value may be left empty: `LET(x,,x)` shows 0,
  as `IF(TRUE,)` does, and elsewhere the name reads as that empty argument would read in its place
  (only `LET(x,,x)` has been observed). Refused on entry, as a Formula that cannot be read is, and as
  Excel's keyboard refuses each: an even number of arguments, a missing calculation, a name bound
  twice, a name that reads as an A1 Reference (`A1`, `R2`, `C3`; `c` and `r` alone are names), `TRUE`
  and `FALSE`. Each binding is computed at most once, when first read, so an
  unused binding's Error Value never reaches the result; but a `LET` depends on everything its text
  names, so an unused binding over a cycle is `#CIRC!`, and one over a waiting Linked Table waits.
  `Table[Column]` always reads the Linked Table, whatever is bound. Two bound names with a space
  between them, Excel's intersection, are refused by name.
- **`INDIRECT` reads A1-style text naming a cell or a range of this Sheet** (ADR-0124, 2026-10-10),
  with or without the Sheet's own qualifier, as it stands after the recalculation; it is volatile,
  as `OFFSET` is. Its text is text: an insertion or a deletion rewrites the References beside it and
  never it, so `INDIRECT("B5")` names B5 whatever moved. It also reads, as Excel does, the forms the
  seventeenth Windows run and the two area passes observed: R1C1 text (`a1` FALSE, 0, a blank or left
  empty) with R and C each a number counted from 1, a leading 0 included (`R3C2`, `R03C2`), an offset
  in brackets from the Formula's own cell, `+`, 0 and `-0` included (`R[1]C[-1]`, `R[+1]C[-1]`,
  `R[0]C[-1]`), or no number, the Formula's own row or column (`RC[-1]`, `R[2]C`); a row part alone a
  whole row and a column part alone a whole column (`R3`, `C[-1]`); two parts of the same kind joined
  by `:` the rectangle they span (`R2:R3`, `RC[-1]:R[1]C[-1]`); one cell followed by `#` its Spill
  Range (`R1C1#`); `A1#`, read as typed `A1#` is; a structured reference's text, read as
  `Table[Column]` typed is, waiting while the table waits, whatever `a1` says; and `#REF!` for text
  that names no place: another Sheet's name in A1 style, a column past `XFD`, A1 text read as R1C1 and
  R1C1 text read as A1, an intersection, a table never declared, a column it lacks, and R1C1 parts of
  two kinds joined (`R2C2:R3`, `R3:C2`). Forms no run has asked — the other combinations of a number,
  an offset and none, the other orders of parts of two kinds joined, `#` after a relative cell,
  another Sheet's name in R1C1 style — are read by the rule those answers settle, and put to the next
  run. `#REF!` too by that rule, though no case asks it: other A1 text that goes on past its
  Reference (the engine's rule, generalised from the intersection), a row past 1048576 in A1 style, as
  `XFE1`'s column is *(2026-10-12, decided with the user: A1 text past the Sheet's edge stays
  `#REF!`)*, and other text that is not R1C1 notation read in R1C1 style, as `B3` is. `#VALUE!` until
  a run observes them, where the observed answers leave the readings apart: R1C1 text with a row or
  column number 0 (`R0C2`); a row or column number past the Sheet's edge (`R1048577C2`, `R2C16385`),
  which could name no place, as `XFE1` does in A1 style, or a place, as an offset past the edge does
  *(2026-10-12, decided with the user: refused until Excel is asked, while A1 text past the edge stays
  `#REF!`, as `XFE1` was observed to be)*; an offset past the Sheet's edge, which Excel reads as a
  place but which no run has shown wrapped round to the far side or stopped at the edge; and, in
  either style, `#` after a range, a row or a column, whatever the parts joined (`A1:A2#`, `A:A#`,
  `R2C2:R3C2#`, `R3#`, `C2#`, `R2C2:R3#`), which could name no place, the first cell's Spill Range,
  the cells themselves or the first cell joined to the second's Spill Range; and `#` inside a join,
  after its first part (`A1#:A2`, `R1C1#:R2C1`), which could name no place, the first cell's Spill
  Range joined to the second part, as `A1#` is read, or the cells themselves with the `#` passed over.
  Another Sheet's name is judged first, so `Sheet2!A1#:A2` is `#REF!`. `#VALUE!` by decision, in
  either style: a declared Linked Table's bare name (`INDIRECT("Positions")`), which Excel reads as
  the table's data, every column and every row without the header. A Formula could then read the
  table by position, and a Linked Table is read by key, never by position (ADR-0049's rule 2)
  *(2026-10-12, decided with the user: the bare name stays `#VALUE!`, in either style)*. Typed, the
  bare name is `#NAME?`, as an unknown name is. What a computed Reference (`INDIRECT`'s or `OFFSET`'s) reads taints the Formula as a
  Reference its text names does: one that reaches a cycle, or its own cell, is `#CIRC!`, and one that
  reaches a waiting cell waits, whatever the function reading it does with an Error Value — `COUNT`,
  `COUNTA`, `SUBTOTAL` and `AGGREGATE`'s options that pass over errors included — and a Reference it
  computes into a Spill Range follows the spill's layout.
- **`SUBTOTAL` and `AGGREGATE` while no row can be hidden** (ADR-0047, 2026-10-10). A Sheet cannot hide
  a row yet, so `SUBTOTAL`'s 101 to 111 give what 1 to 11 give, and `AGGREGATE`'s options 1, 3, 5
  and 7 what 0, 2, 4 and 6 give, as Excel's do over such a sheet; a test fails on the day a row can
  be hidden. A cell is nested when its Formula, or its Spill Range's Anchor's, calls `SUBTOTAL` or
  `AGGREGATE` anywhere, as the seventeenth Windows run read Excel's answers: `SUBTOTAL` and
  `AGGREGATE`'s options 0 to 3 pass over it, and options 4 to 7 count its Value. A function number or
  option that is not whole is truncated (`SUBTOTAL(9.5,…)` is `SUBTOTAL(9,…)`), and `AGGREGATE`'s
  array form takes a Value given directly as an array of that one Value: a number is that number, and
  text or a boolean holds none (`#NUM!`). `#VALUE!`: an argument that is no reference when it is
  computed, and `AGGREGATE`'s function numbers for functions the engine does not support: 13
  (`MODE.SNGL`) and 16 to 19 (`PERCENTILE` and `QUARTILE`), where Excel answers (AGGREGATE-027 to 031
  record its answers).
- **More of the refusal of unlifted functions.** `CONCATENATE` over a range of several cells, and a
  `TEXTJOIN` delimiter of several cells, are `#VALUE!`; `CONCAT` and `TEXTJOIN`'s texts take ranges.

### Domains admitted from the October 3 Excel observations

The 21 additions above follow ADR-0047's permission to answer part of a function's domain.
An argument outside these domains gives `#VALUE!` unless another Error Value is stated:

- `SUMIF`, `SUMIFS`, `COUNTIF`, `COUNTIFS`, `AVERAGEIF`, `AVERAGEIFS`, `MAXIFS`, `MINIFS` and
  `COUNTBLANK` take References or Linked Columns as their ranges. Computed arrays are refused.
  IFS ranges must have the same rows and columns. With literal criteria and result References,
  `SUMIF` and `AVERAGEIF` resize the result to the criteria range's shape; the footprint participates in
  recalculation and cycle detection. Resizing a computed Reference is refused. Whole-column
  counts include absent cells without materialising the column.
- Criteria distinguish an absent cell, formula-empty text, numbers, numeric text, booleans and
  Error Values. Equality and inequality of ASCII text support `*`, `?` and `~` escapes; numeric
  comparisons retain the observed distinctions between Values. Text relational ordering and
  non-ASCII matching are refused. Criteria over 255 characters, numeric text over 15 mantissa
  digits, and culture-sensitive spellings with commas, currency, percentage or date/time syntax
  are refused. Decimal-point numeric text is admitted only for cultures with a decimal point.
  An Error Value's spelling, such as `#N/A`, remains a valid equality criterion. `#CIRC!` and
  `#GETTING_DATA` retain their strict propagation through all References.
- `VLOOKUP`, `HLOOKUP` and `MATCH` admit exact matching of ASCII text, including wildcards, and
  numbers or booleans. Approximate lookup requires a sorted vector of one kind, without blanks
  or Error Values; ordered text contains only ASCII letters, digits and spaces. Lookup text
  over 255 characters, a match after an Error Value, and an unmatched search containing errors
  other than `#N/A` are refused. Descending MATCH with a repeated nearest larger key is refused
  when the key is not equal. Their duplicate choices do not change XLOOKUP's distinct rules.
- `MROUND` admits integer multiples. A nonzero fractional multiple of the same sign as a
  nonzero number is refused; opposite signs give `#NUM!` and zero gives zero. This preserves
  the unresolved decimal-midpoint cases rather than guessing a tolerance. `CEILING.MATH` and
  `FLOOR.MATH` admit the observed significance and negative-number modes. A quotient that is
  nonintegral in binary64 but integral at 15 significant digits is refused with `#VALUE!` until
  that boundary is observed; division underflow of a nonzero number is `#NUM!`.
- `YEARFRAC` admits bases 0–4, including the observed February and 1900 boundaries. `DATEDIF`
  admits `Y`, `M`, `D` and `YM`; `YD` only before the first completed anniversary. `MD` and longer
  `YD` intervals are refused. Both discard the time portion of their dates.
- **Before 1 March 1900**, `EDATE` and `EOMONTH` count months on the real calendar, where
  February 1900 has 28 days (`EOMONTH(31,1)` is 59), and `NETWORKDAYS` and `WORKDAY` give each
  serial the day of the week `WEEKDAY` gives it, serial 1 a Sunday and serial 60 a Wednesday
  (`NETWORKDAYS(30,100)` is 51), as Excel was observed to. A `WORKDAY` that falls before serial 0
  is `#NUM!`. `EDATE` and `EOMONTH` read a start of serial 0 or 60 as `MONTH` and `DAY` read it, in
  January 1900 and on the 29th of February: `EOMONTH(0,1)` is 59 and `EDATE(60,1)` is 89, and from
  serial 0 `EDATE` keeps day 0, the day before the 1st, so `EDATE(0,1)` is 31. A date between -1 and
  0 is cut down to -1, and is `#NUM!`, under `YEAR`, `MONTH`, `DAY`, `WEEKDAY`, `DAYS`,
  `NETWORKDAYS`, `WORKDAY`, `EOMONTH` and `EDATE`, as Excel was observed to answer at -0.5
  (YEAR-008, MONTH-005, DAY-005, WEEKDAY-008, DAYS-005, NETWORKDAYS-009, WORKDAY-010, EOMONTH-016
  and 017, EDATE-017). `DATEDIF`, `YEARFRAC`, `HOUR`, `MINUTE` and `SECOND` give `#NUM!` for a
  serial of -1 or below, which they do not cut down. Excel was observed to at -1 under three of them
  (DATEDIF-045, YEARFRAC-056, MINUTE-002); below -1, and under `HOUR` and `SECOND` at all, it is the
  engine's answer, by generalisation. Between -1 and 0 their readings part — cut down to -1, `#NUM!`,
  or read as a date or a time of day, an answer — and they refuse it with `#VALUE!` until Excel
  is asked (DATEDIF-048, YEARFRAC-058, HOUR-005, MINUTE-003, SECOND-003).
- `UPPER`, `LOWER` and `PROPER` admit ASCII plus `éÉßẞıİǱǲǳﬃ`, nonbreaking space and `σςΣ`.
  `LOWER` also admits `ΑΟ`; a word containing `Σ` must contain only `ΑΟΣ`, with words separated
  by ASCII spaces. `PROPER` admits sigma only as a single-letter word separated by ASCII spaces.
  `SEARCH` admits ASCII plus `éÉßıİσςΣ`, with its own observed case matching and wildcard rules.
  Other characters, supplementary-plane text and other sigma contexts are refused. These
  explicit mappings do not depend on the operating system's Unicode casing tables.

`IRR`, `XIRR` and `RATE` remain undeclared (`#NAME?`). The solver experiment in
`spikes/exsheet-financial-solvers/` does not yet reproduce Excel's chosen roots, stopping results
and failures. `NPV` is undeclared too (ADR-0202, note of 2026-10-12): no evaluation in
`spikes/exsheet-financial-evaluation/` gives every double Excel gave for it. The October 3
observation run did not resolve existing Supported functions' other `uncertain` corpus cases.

## Operations, and undoing them

Every user operation is a `SheetEdit`, and `Sheet.Do` returns the one `SheetStep` that undoes and
redoes it, each with one recalculation. The stack of steps is the caller's.

```csharp
var step = sheet.Do(SheetEdit.InsertRows(row: 2));   // References are rewritten to keep naming the same cells
step.Undo();                                         // Entries, formats and References exactly as they were
```

- **Insertion and deletion** of rows and columns rewrite every Reference, relative and absolute
  alike; a range grows or shrinks as Excel's does, and a Reference whose cells are all deleted is
  written `#REF!` in the stored Formula. An insertion that would push an Entry off the Sheet's
  edge is refused (`SheetRefusedException`), as Excel refuses it; one that pushes only the cells a
  Reference names off is done, as in Excel, with a range cut at the edge and a Reference left
  with no cells written `#REF!`. Inserted rows take the Number Format, Alignment, Font and Fill
  of the row above — its cells' and the row's own — and inserted columns those of the column to
  the left, as Excel's default does; Entries are never copied. Borders are not taken, and every
  cell moves with its own four sides: the first inserted row's top shows the line on the bottom of
  the row above, and a line on row 1's top moves down with row 1 (columns alike). Where a deletion
  brings two rows together and both record a line on their edge, the upper row's is shown (the
  left column's for columns). Formats set on rows and columns move with them, and so do column
  widths.
- **Column widths** are part of the Sheet Document, in characters as Excel counts them.
  A width is one of three kinds (ADR-0046, 2026-09-28): **default** (never set, not recorded),
  **widened by entry** (`SheetColumnWidthKind.WidenedByEntry`: an entry widened the column to it;
  a longer entry widens it again) or **set by the user** (`SheetColumnWidthKind.SetByUser`: a
  drag, a size to fit, a command; an entry never widens it). Both recorded kinds are marked
  custom (`IsCustom`), as Excel's file marks them (`customWidth`).
  `SheetEdit.SetColumnWidth(range, width)` sets the user's width on every column the range spans,
  from more than 0 to 255 characters; `null` puts them back at the default width, which is not
  recorded. `SheetEdit.SetAutomaticColumnWidth(range, width)` records a width widened by entry.
  `Sheet.GetColumnWidth(column)` is the recorded `SheetColumnWidth` — `Width`, `Kind`,
  `IsSetByUser` and `IsCustom` — or `null`. An inserted column takes the width of the column to
  its left, of its kind, as it takes its formats; a deleted column's width goes with it; undo puts
  back both. `SheetChange.Columns` names the columns whose width, or its kind, a change (or its
  undo) changed. A version 4 document recorded only widths the user set, and they open as the
  user's; a version 5 document's custom widths open as the user's and its others as widened by
  entry.
- **A Cell Format** — Number Format, Alignment, Font, Fill and Borders — is recorded at three
  levels, cell over row over column, as in Excel. `SheetEdit.SetNumberFormat(range, format)` and
  `SheetEdit.SetAlignment(range, alignment)` record whole columns (`CellRange.Parse("B:D")`) and
  whole rows (`"2:4"`) as one entry each, and the whole Sheet (`"A:XFD"`) as one run; a cell
  inside that records its own Number Format takes the new one, as Excel's do. Any other range
  sets its cells. `SheetEdit.SetCellFormat(ranges, change)` sets the parts a `CellFormatChange`
  names on several ranges — a selection of several rectangles, whole columns and rows among them
  — as one step; every part it does not name stays as each cell has it, each Font emphasis on its
  own, and its `BorderChange` (outline, inside, each edge, none) is relative to each range, so
  each range gets its own outline. Each cell records its own four sides, as Excel's files do. An
  outer edge is recorded on the range's cells, and the cells beside it lose their record of the
  same edge, at whichever level they lie, so the later setting wins from either side. A copy, a
  paste, Ctrl+D, Ctrl+R and the fill handle write each target's own four sides and touch no
  neighbour. An outline over whole columns sets their left and right edges, over whole rows their
  top and bottom and the left of column A, and over the whole Sheet nothing. `Sheet.GetCellFormat`
  answers what a cell shows, and `GetNumberFormat`, `GetAlignment`, `GetFont`, `GetFill` and
  `GetBorders` one part of it. Its Borders are the edges as shown, the same from either cell:
  where both cells record a line on an edge, the upper cell's (the left cell's) is shown, and
  otherwise whichever records one. `GetCellFormats(range)`, what Format Cells reads, answers each
  cell's own sides instead, as Excel's Format Cells compares them inside a range, and
  `GetEdgeLines(range)` the lines drawn along the range's outer edges, as Excel's Format Cells shows
  them. `GetRowNumberFormat`, `GetColumnNumberFormat` and their
  alignment counterparts answer what a level records. A colour (`CellColour`) is Automatic or RGB; a Fill is RGB or none; a Border
  is one of Excel's thirteen line styles and a colour. The per-cell forms taking addresses keep
  working.
- **Built-ins in the culture's own form.** A Number Format is written in Excel's invariant codes,
  and Excel's built-in short date, date with the month's name, hour and minute and currency show
  in the Sheet culture's own form: `05-Jan-26`, `09:05` and `£5.00` under en-GB. Under ja-JP
  `mmm` is the month's number in every code, as Windows names the months there (`05-1-26`).
  `NumberFormat.LocalCode(culture)` spells a format as the culture's Format Cells does, and
  `NumberFormat.TryParseLocal(code, culture, …)` reads a code typed there: it is a built-in only
  when it spells that built-in's code under the culture, so under ja-JP `dd-mmm-yy` is the
  built-in `d-mmm-yy`, and `d-mmm-yy` typed there is a code of its own, recorded as `d\-mmm\-yy`.
- **Copy and paste.** `Sheet.Copy(range)` gives the Entries (`SheetBlock`) for a paste inside
  the Sheet, where relative References shift by the distance pasted, and the Values for anywhere
  else: `Text` as the cells show them, `Html` unformatted. `SheetEdit.PasteText` reads each
  pasted field as if typed under the Sheet's culture, except that a field that cannot be read as
  a Formula (`=1+`) is taken as text, as Excel takes it. A paste that writes in parts is one
  edit, one step and one change (ADR-0180): `SheetEdit.Paste(block, targets)` over several ranges,
  and `SheetEdit.PasteText(fields, entries)` for typed fields beside Entries kept exactly. A copy
  that reaches a `#GETTING_DATA` Value is refused.
- **Fill.** `SheetEdit.Fill(source, target, direction)` fills as Excel does for copies (with
  References shifted), a linear trend from two or more numbers (held at 15 significant digits,
  as Excel holds it), and a single date by day, and
  refuses every other pattern — `Item 1`, day and month names, several dates — rather than fill
  it with copies.
- `Sheet.Check(edit)` says whether an edit would be refused, without doing it.

## Linked Tables

Data the application holds reaches Formulas as a Linked Table, pushed whole and read by
structured reference and by key:

```csharp
sheet.DeclareLinkedTable("Positions", ["Id", "PV"], key: "Id");
sheet.Enter(CellAddress.Parse("A1"), "=XLOOKUP(\"R-4471\", Positions[Id], Positions[PV])");
// A1 is #GETTING_DATA until the first snapshot arrives.
sheet.PushLinkedTable("Positions", [[Value.FromText("R-4471"), Value.FromNumber(250.5)]]);
```

A snapshot replaces the last in one step and recalculates only the Formulas that read the table.
The Sheet Document records each table's declaration — its name, column names and key — and never
its rows: a Sheet opened from one already has the tables declared, and their readers show
`#GETTING_DATA` until the first snapshot is pushed. Declare your tables at start-up regardless:
the same declaration again changes nothing, and one with other columns or another key replaces
the held one, dropping its rows so readers wait again. A table is never undeclared.

The **key** is optional, and names one of the columns. Every snapshot of a keyed table is checked:
no key may appear twice, compared as `XLOOKUP`'s exact match compares, so `r-4471` and `R-4471` are
one key while the number 1 and the text `1` are two. A blank key is not a key. A snapshot in which a
key repeats throws `RepeatedKeyException`, naming the table, the key column and the key. The
previous snapshot is not kept: the table waits again, and every reader shows `#GETTING_DATA`, which
`IFERROR` does not catch. `RepeatedKeyException.Change` says which cells changed with it. A table
whose rows are told apart by several columns needs a column that joins them (`ACME|5Y`) as its key.
A column the table does not have is `#REF!`; a column used where one Value is wanted gives its
Value when it has exactly one row, and `#VALUE!` otherwise.

## Formula entry

`FormulaEntry` answers, over a Formula's unfinished text and its caret, what an editor needs:
completion candidates on Excel's triggers — names once a letter is typed (`Sheet.Complete` adds the
Sheet's Linked Tables to the functions), a table's columns after `Table[`, and an argument's values
where it takes one of a fixed list, before anything is typed — the argument hint, whether a Reference can be written at the caret (Point mode), the Reference
text for a range, what F4 makes of the Reference at the caret, and every Reference in the text
with the cells it names or the Linked Table column it reads, for Reference Outlines
(`Sheet.References` reads a Sheet qualifier against the Sheet's own name). For a Pointing Scope
(ADR-0058) it writes what reads a Linked Table's cell by key, `LookupText` —
`XLOOKUP("R-4471", Positions[Id], Positions[PV])`, or for a key of several columns
`XLOOKUP(1, (Cds[Entity]="ACME")*(Cds[Tenor]="5Y"), Cds[Spread])` — a column's structured reference,
`StructuredReferenceText`, and a Value as Excel writes a constant of its kind, `ConstantText`.

## More

The decisions behind the engine are ADR-0046 to ADR-0051 in
[`docs/adr/`](https://github.com/AkiraMisawa/ex-grid/tree/main/docs/adr), and the terms — Sheet,
Entry, Value, Formula, Reference, Error Value, Sheet Document — are defined in
[`CONTEXT.md`](https://github.com/AkiraMisawa/ex-grid/blob/main/CONTEXT.md).
