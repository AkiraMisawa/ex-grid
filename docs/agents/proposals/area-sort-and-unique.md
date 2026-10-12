# The area passes, sort and unique: what the orchestrator writes

Branch `claude/area2-sort-and-unique`, on `claude/area-corrections` (which holds the first round,
`claude/area-sort-and-unique`). The evidence: the first area pass (`verification/2026-10-12-windows-area/`,
`results-2026-10-12-area-com-en-US.json`) and the second (`verification/2026-10-12-windows-area-2/report.md`,
`results-2026-10-12-area-2-com-en-US.json`, Excel 16.0.20430.20146 under en-US through COM). This file replaces every
edit its earlier versions proposed; none of those has been applied. It holds the text this area would put into
ADR-0125, ADR-0047, the engine's README and the Docs Site, which a background agent does not change. Each edit is
exact: the old lines, then the new, whole lines with the target's own indentation.

The branch edits itself: the engine, the corpus (`sort.json`, `sortby.json`, `unique.json`, `filter.json`,
`transpose.json`, `iferror.json`), `tests/ExSheet.Engine.Tests/SortAndUniqueBlankTests.cs` and `SpillTests.cs`, the
catalogue's `IFERROR`, `FILTER`, `SORT`, `SORTBY`, `UNIQUE` and `TRANSPOSE` rows (`docs/specs/exsheet-functions/spec.md`),
and the area's four Change Notes.

Every answer below is marked by where it comes from: **observed** (Excel's answer, by the case named), **by
agreement** (every reading the observed answers leave gives the one answer, so no run is needed to choose), or **by
generalisation** (a rule the observed answers support, carried to a function or form no run has asked; the case that
asks is named). Nothing by agreement or generalisation is Excel's answer until its case is `observed`.

## 1. What Excel answered, and what the engine does now

The `lookupData` fixture has Q1 = 7, Q2 blank and H1:H2 = 10, 20.

| Case | Formula | Excel (second pass) | Before the passes | Now |
|---|---|---|---|---|
| SORT-015 | `=SORT(A1:A2)` over 7 and then `=NA()` | 7 | `#VALUE!` (refused) | 7, `observed` |
| SORT-021 | `=COUNTA(SORT(Q1:Q2))` | 2 | `#VALUE!` (refused) | 2, `observed` |
| SORT-022 | `=ISBLANK(INDEX(SORT(Q1:Q2),2))` | TRUE | `#VALUE!` (refused) | TRUE, `observed` |
| UNIQUE-017 | `=COUNTA(UNIQUE(Q1:Q2))` | 2 | `#VALUE!` (refused) | 2, `observed` |
| UNIQUE-018 | `=INDEX(UNIQUE(Q1:Q2),2)=""` | TRUE | `#VALUE!` (refused) | TRUE, `observed` |
| UNIQUE-019 | `=SUM(--(LEN(UNIQUE(A1:A3))>0))` over a, blank, b | 2 | `#VALUE!` (refused) | `#VALUE!`, `observed`, recorded with `excelExpect` 2 |

The 16 agreements `-Update` promoted (SORT-016 to 020, 023 to 025; SORTBY-007 to 009; UNIQUE-012 to 016) are unchanged
in what they expect; their descriptions, and those of SORT-009, SORT-010, SORTBY-006, UNIQUE-007 and UNIQUE-008, no
longer say "Engine's answer, Excel to be asked". This round also drops that prefix from FILTER-009 to 011 and
TRANSPOSE-004, which the seventeenth run observed (`results-2026-10-11-run17-update-en-US.json`: each `agree`).

### The blank read inside the Formula

Three readings were open for a blank `UNIQUE` or `SORT` keeps from its array, read inside the Formula: the 0 it
spills as, empty text, or the empty value a blank cell holds.

- **The 0 it spills as** is ruled out: `INDEX(UNIQUE(Q1:Q2),2)=""` is TRUE (UNIQUE-018), and 0 is not equal to empty
  text; `LEN` reads it as 0 (UNIQUE-019's 2 over a, blank, b is 1, 0, 1), where `LEN(0)` is 1; and `ISBLANK` of it is
  TRUE (SORT-022).
- **Empty text** is ruled out for `SORT` by `ISBLANK` (SORT-022), and for `UNIQUE` by its spill: an element of empty
  text spills as empty text, and UNIQUE-010 read 0.
- **The empty value a blank cell holds** fits all six answers. And `COUNTA` counts it in the array (SORT-021,
  UNIQUE-017: 2 over 7 and a blank), where it passes over a blank cell of a range.

The engine marks that blank on the operand (`KeptBlank`, the code's word, not the glossary's) and reads it so:

- **Observed:** `COUNTA` counts it in `SORT`'s and `UNIQUE`'s array (SORT-021, UNIQUE-017); `INDEX` takes it out still
  blank, which `ISBLANK` (SORT-022) and `=` (UNIQUE-018) read as a blank cell's; it spills as 0 (SORT-012, UNIQUE-010,
  016; `TRANSPOSE`'s too, TRANSPOSE-004); `ROWS` counts it in `UNIQUE`'s extent (UNIQUE-011, 012).
- **By generalisation:** `SORTBY`, `FILTER` and `TRANSPOSE` keep a blank as `SORT` does, so theirs is taken to be the
  same blank and read as `SORT`'s is (SORTBY-011 to 013, FILTER-012 and 013, TRANSPOSE-005 and 006 ask). Every other
  function that reads one Value — `LEN`, `ISTEXT`, `ISNUMBER`, `ISERROR`, `VLOOKUP`'s and `XLOOKUP`'s lookup value,
  and the rest that read through `TryScalar` — reads the blank `INDEX` takes out as a blank cell's, as only `ISBLANK`
  was seen to; `LEN` over the array (UNIQUE-019) is consistent with it but is refused, unlifted. Every operator reads
  each Value so, as only `=` was seen to (UNIQUE-024 asks `&`). The engine has no `N` function, so `N` is no reader
  here.
- **By agreement:** `SUMPRODUCT` (anything but a number is 0 inside its arrays, as Microsoft documents) and `CONCAT`
  (nothing either way) give the one answer whether they pass over it or read it as the empty value of their kind.
  `SUM` gives the one sum either way except where the blank would be its last number: the last addition decides
  whether a sum that nearly cancels is 0 (ADR-0047, second run), so `SUM` computes both and refuses where they
  differ (SORT-033 and 037 ask). `VLOOKUP`, `HLOOKUP` and `XLOOKUP` take an array that holds the blank where it is
  neither a key they search nor what they return: there no reading of it reaches the answer.
- **Refused, where the readings part:** `AVERAGE` (7 or 3.5 over 7 and a blank), `MIN`, `MAX`, `COUNT`, `PRODUCT`,
  `AND`, `OR`, `TEXTJOIN` (with `ignore_empty` FALSE an empty item or none; with TRUE every reading agrees, and it
  refuses there too, as the first round left it), `COUNTA` given the blank alone as one Value (counted as in the array,
  or not as a blank cell's), `MATCH` and `XMATCH` and a lookup whose keys hold it or whose target it is, and another of
  the five reading it (`SORT(FILTER(...))`: as a key it sorts last as a blank cell's does, or first as 0; SORT-038
  asks). `IF`, `CHOOSE`, `IFS`, `SWITCH`, `IFERROR` and `IFNA` refuse it too (section 9). The refusal is the Formula's
  (`Evaluator.Refuse`), so `IFERROR` cannot catch it and `COUNTA` cannot count it, as a Formula that meets `#CIRC!` is
  `#CIRC!` (ADR-0047).

### `FILTER` and `TRANSPOSE`

Until this round they kept a blank unmarked, and a Formula read it as a blank cell: `=COUNTA(FILTER(Q1:Q2,H1:H2>0))`
was 1, while `=COUNTA(SORT(FILTER(Q1:Q2,H1:H2>0)))` was 2. A reorder cannot change a count, so one of the two was a
plausible wrong answer. Their result is now marked as `SORT`'s is: `COUNTA` counts the blank (FILTER-012 and
TRANSPOSE-005 are 2, by generalisation), `INDEX` takes it out blank (FILTER-013, TRANSPOSE-006), and the readers that
part refuse (FILTER-014, TRANSPOSE-007), `COUNTA(SORT(FILTER(...)))` among them. A Formula over a filtered range that
holds a blank, `=AVERAGE(FILTER(A2:A9,B2:B9="x"))` for one, is `#VALUE!` where it used to answer.

### The Error Value key: the rule SORT-015 settles, and where it stops

Excel's `=SORT(A1:A2)` over 7 and then `#N/A` shows 7. So an Error Value key is not `SORT`'s result, and does not
sort before the numbers. The `#N/A` followed the 7 already, so the answer fits a key that sorts after the numbers and
one that keeps its place alike, and leaves open where it sorts against text, booleans and blanks, whether a descending
sort puts it first, last or where it stood, and how two different Error Values order (kept in order, or by
`ERROR.TYPE`).

`SORT` answers a key only where every one of those readings gives the one order: its Error Values all one, and beside
them nothing, or, sorted ascending, numbers alone, none of them after an Error Value — the shape of SORT-015. The line
goes with its key; an Error Value outside the key column is carried as any Value is. Everywhere else the Formula is
`#VALUE!`: with a number after the Error Value (SORT-026: 7 if it sorts after the numbers, `#N/A` if it keeps its
place), and where SORT-028 to 032 ask. `SORTBY` refuses every Error Value key: a `by_array`'s could as well be the
result, as `FILTER`'s `include`'s is (SORTBY-010 asks). Both refusals are now the Formula's, so
`=IFERROR(SORT(A1:A2),0)` and `=COUNTA(SORT(A1:A2))` over `=NA()` and 7 are `#VALUE!`; with the refusal an Error Value
the call returned, they would be 0 and 1, as they were before the area passes.

### The refusal of an unlifted function is the Formula's

ADR-0125 keeps an array where an unlifted function wants one Value "`#VALUE!` … an Error Value, never another answer",
and IFERROR-009 records the decision that `IFERROR` does not turn that refusal into a fallback. Given the range itself,
`IFERROR` refused; given a call that refused, it caught the `#VALUE!`: `=IFERROR(LEN(A1:A3),0)` was 0, and so were
`ISERROR(ROUND(A1:A3,0))` TRUE, `COUNTA(ROUND(A1:A3,0))` 1 and `COUNT(IF(A1:A3>1,1))` 0 — answers about the
`#VALUE!` the engine made, where Excel 365 applies the function to each Value (ADR-0125). The refusal is now the Formula's (`FunctionLibrary.Unlifted`, which calls
`Evaluator.Refuse`), as ADR-0047 makes a Formula that meets `#CIRC!` `#CIRC!`, at every place a function refused an
array where it wants one Value: `TryScalar` (and so every `TryNumber` and `TryText`), `IF`, `IFS`, `IFERROR`,
`IFNA`, `ISERROR`, the IS functions, `ERROR.TYPE`, `ROUND`, `XLOOKUP`'s and `XMATCH`'s lookup value and modes, and a
criterion of the conditional aggregates. IFERROR-017 records it.

No search: no digits decide in this area. The candidates are orders and readings, each listed above with the case
that rules it out or asks it.

## 2. The code

- `Operand.cs`: `KeptBlank`, `HoldsBlank` and `WithKeptBlank()`: the operand holds a blank `UNIQUE`, `SORT`, `SORTBY`,
  `FILTER` or `TRANSPOSE` kept from its array, as one of its Values or as the one Value `INDEX` picked.
- `FunctionDefinition.cs`: `FunctionCall.Operand` refuses the Formula when the argument holds a kept blank;
  `KeepingBlanks` gives it to the readers above; `OneValue` gives a kept blank Value to a function that reads one
  Value, and refuses an array holding one. `Measured` evaluates the argument for its extent.
- `Evaluator.cs`: `Refuse` and the Formula's refusal, now also for unlifted functions and Error Value keys.
- `Functions.Arrays.cs`: `Made` marks the result of all five; `TryOrderLines` takes `errorKeysOrdered` (`SORT` true,
  `SORTBY` false) and `ErrorKeysSettled`, which now also asks that no number follow an Error Value; `SORT` and `SORTBY`
  refuse an unsettled key with `Evaluator.Refuse`; `SUMPRODUCT` reads through `KeepingBlanks`.
- `Functions.cs`: `Unlifted`; `COUNTA` counts every element of an array holding a kept blank, and refuses one given as
  one Value; `SUM` adds its numbers both ways (`AddNumbers`) and refuses where the sums differ; `XLOOKUP` reads its
  return array through `KeepingBlanks` and refuses where what it returns holds the kept blank; `Search` reads the
  modes through `TryNumber`. `Functions.LegacyLookup.cs`: `VLOOKUP` and `HLOOKUP` read the table through
  `KeepingBlanks` and refuse where the kept blank is among the keys or is the target. `Functions.Logic.cs`,
  `Functions.Lookup.cs`, `Functions.Criteria.cs`, `Functions.Arguments.cs`: the unlifted refusals go through
  `Unlifted`. `Functions.Text.cs`: `CONCAT` through `KeepingBlanks`.

## 3. Tests and counts

`SortAndUniqueBlankTests.cs` (each test named with ADR-0125, or ADR-0047 for the Error Value key), 131 rows to 171: the
Error Value key answered where it follows every number (5 rows), its line going with it, refused where the readings
part (12 rows, three with a number after it), its refusal not caught (5 rows); `COUNTA` counting a kept blank of all
five (11 rows); one-Value readers of the blank `INDEX` takes out (20 rows); `SUM`, `SUMPRODUCT` and `CONCAT` taking it
(9 rows); lookups answering where they never read it (5 rows) and refused where they do (7 rows); the readings that
part refused (27 rows) and not caught (8 rows); and the rest as before. `SpillTests.cs`, 35 rows to 52: an unlifted
function's refusal is the Formula's (13 rows), and a call never evaluated refuses nothing (4 rows).

With `Unlifted` returning a plain Error Value, 13 tests and corpus cases fail. With the new order condition of
`ErrorKeysSettled`, the Formula's refusal of an Error Value key, and the marks on `FILTER`'s and `TRANSPOSE`'s results
taken out, 28 fail.

- `tests/ExSheet.Engine.Tests`, the whole project: **5330 passed, 0 failed, 3 skipped** (5266 passed on `22e664dd`:
  7 new corpus cases, 40 new rows in `SortAndUniqueBlankTests`, 17 in `SpillTests`).
- `tests/ExSheet.Components`: **752 passed, 0 failed**.

## 4. Cases changed and added (`uncertain` with the engine's answer as `expect`, unless marked)

| Case | Formula | The engine | What it asks |
|---|---|---|---|
| SORT-015 (`observed`) | `=SORT(A1:A2)` over 7, `=NA()` | 7 | description only: it fits an Error Value sorted after the numbers and one that keeps its place alike |
| SORT-026 (changed) | `=SORT(A1:A2)` over `=NA()`, 7 | `#VALUE!` (was 7) | 7 if an Error Value key sorts after the numbers, `#N/A` if it keeps its place |
| SORT-027 | `=SORT(A1:A2)` over 7, `=NA()`, at ZZ1001 | `#N/A` | the Error Value key's own cell shows it, where both readings put it |
| SORT-028 | `=SORT(A1:A2,1,-1)` over 7, `=NA()` | `#VALUE!` | descending: 7 if the Error Value stays last or keeps its place, `#N/A` if it comes first |
| SORT-029 to 032 | as before | `#VALUE!` | an Error Value key beside text, a boolean, a blank, another Error Value |
| SORT-033 to 037 | as before | as before | how `SUM`, `COUNT` and `COUNTA` read a kept blank, and what a Formula whose result it is shows |
| SORT-038 (new) | `=SORT(FILTER(Q1:Q2,H1:H2>0))` | `#VALUE!` | 7 if `SORT` sorts the blank `FILTER` keeps last, as a blank cell's key (SORT-011), 0 if it reads it as 0 |
| SORT-039 (new) | `=ISBLANK(INDEX(IF(TRUE,SORT(Q1:Q2)),2))` | `#VALUE!` | TRUE if `IF` hands `SORT`'s kept blank on as it is, FALSE if as the 0 it spills as |
| SORTBY-010 | `=SORTBY(H1:H2,Z1:Z2)`, Z1 7, Z2 `=NA()` | `#VALUE!` | 10 if `SORTBY` leaves the Error Value key after the 7 as `SORT` did, `#N/A` if it is the result |
| SORTBY-011 to 013 | as before | 2, TRUE, 0 | `SORTBY`'s kept blank counted, blank to `ISBLANK` and spilled as 0, as `SORT`'s was |
| UNIQUE-019 (`observed`) | `=SUM(--(LEN(UNIQUE(A1:A3))>0))` | `#VALUE!` | its decision now says the unlifted refusal is the Formula's, kept blank or not |
| UNIQUE-020 to 024 | as before | as before | `UNIQUE`'s kept blank read as `SORT`'s |
| FILTER-009 to 011 (`observed`) | as before | as before | description only: the stale "Excel to be asked" removed |
| FILTER-012 (changed) | `=COUNTA(FILTER(Q1:Q2,H1:H2>0))` | 2 (was 1) | 2 if `FILTER` keeps the blank as `SORT` does, 1 if `COUNTA` passes over it as over a blank cell |
| FILTER-013 (new) | `=ISBLANK(INDEX(FILTER(Q1:Q2,H1:H2>0),2))` | TRUE | FALSE if `FILTER` turned the blank into the 0 it spills as |
| FILTER-014 (new) | `=AVERAGE(FILTER(Q1:Q2,H1:H2>0))` | `#VALUE!` | 7 if `AVERAGE` passes over the kept blank, 3.5 if it reads it as 0 |
| TRANSPOSE-004 (`observed`) | `=TRANSPOSE(Q1:Q2)` at AAA1000 | 0 | description only |
| TRANSPOSE-005 (changed) | `=COUNTA(TRANSPOSE(Q1:Q2))` | 2 (was 1) | as FILTER-012, for `TRANSPOSE` |
| TRANSPOSE-006 (new) | `=ISBLANK(INDEX(TRANSPOSE(Q1:Q2),1,2))` | TRUE | as FILTER-013 |
| TRANSPOSE-007 (new) | `=MIN(TRANSPOSE(Q1:Q2))` | `#VALUE!` | 7 if `MIN` passes over the kept blank, 0 if it reads it as 0 |
| IFERROR-017 (new, recorded) | `=IFERROR(LEN(A1:A3),0)` over a, blank, b | `#VALUE!` | `engineDiffersByDecision` ADR-0125; Excel spills `LEN` of each Value, which the run records |

The next `-Area` pass names `sort`, `sortby`, `unique`, `transpose`, `filter` and `iferror`.

## 5. What remains unsettled, and why

- **Where an Error Value key sorts** with a number after it (SORT-026), against text, booleans and blanks, in a
  descending sort, and against another Error Value (SORT-028 to 032), and whether `SORTBY` sorts one at all
  (SORTBY-010). SORT-015 is one ascending pair whose Error Value was already last.
- **How a function that reads an array whole reads a kept blank**, where passing over it and reading it as an empty
  value part: `AVERAGE`, `MIN`, `MAX`, `COUNT`, `PRODUCT`, `AND`, `OR`, `TEXTJOIN`, a search, another of the five, and
  `SUM` where the last addition moves (SORT-034, 037, 038, UNIQUE-021, 022, FILTER-014, TRANSPOSE-007 ask some), and
  whether `IF` hands it on as it is (SORT-039).
  `COUNTA` given the blank alone (SORT-035, UNIQUE-023).
- **Whether `SORTBY`, `FILTER` and `TRANSPOSE` keep the blank `SORT` keeps** (SORTBY-011 to 013, FILTER-012 and 013,
  TRANSPOSE-005 and 006), and whether a function that reads one Value other than `ISBLANK` reads it as a blank cell's
  (UNIQUE-020, 024 ask two).
- **`LEN` and the functions not yet applied to each Value of an array**: UNIQUE-019 and IFERROR-017 stay recorded until
  ADR-0125's lifting is built.

## 6. ADR-0125

### 6a. A dated note after the note of 2026-10-12, "What becomes Excel's answer"

Old (the end of that note):

```
guess where the blank goes, what its spilled cell shows, how a descending sort places it and whether `UNIQUE` counts
it one of a kind with 0; SORT-012 to 014 and UNIQUE-010 and 011 ask those.)*
```

New:

```
guess where the blank goes, what its spilled cell shows, how a descending sort places it and whether `UNIQUE` counts
it one of a kind with 0; SORT-012 to 014 and UNIQUE-010 and 011 ask those.)*

*(2026-10-12, after the two area passes, `verification/2026-10-12-windows-area/` and `verification/2026-10-12-windows-area-2/`:
**a blank among `SORT`'s and `SORTBY`'s keys, and in `UNIQUE`'s array, is answered, and so is the blank they keep,
read inside the Formula, where Excel's answers or the agreement of every reading settle it.** The refusal of the note
above is withdrawn. What Excel answered: over 7 and a blank, `=SORT(Q1:Q2)` spills 7 and then 0, ascending or
descending, and over b, a blank and a, `=SORT(A1:A3)` is a first: a blank key sorts after every other, in either
order, neither as 0 nor as empty text, and blank keys keep their order (SORT-011 to 014, 016 to 020, 023 to 025).
`SORTBY` sorts a blank key last so, key by key, ascending (SORTBY-007 to 009). In `UNIQUE` a blank is one of a kind
only with another blank, never with 0 or empty text (UNIQUE-011 to 015). A blank spills as 0 (SORT-012, UNIQUE-010,
016, and `TRANSPOSE`'s, TRANSPOSE-004). Read inside the Formula, the blank `SORT` and `UNIQUE` keep is the empty value
a blank cell holds: `=ISBLANK(INDEX(SORT(Q1:Q2),2))` is TRUE (SORT-022), `=INDEX(UNIQUE(Q1:Q2),2)=""` TRUE
(UNIQUE-018), and `=SUM(--(LEN(UNIQUE(A1:A3))>0))` over a, a blank and b is 2 (UNIQUE-019): not the 0 it spills as,
which would not equal empty text, nor empty text, which would spill as itself. `COUNTA` counts it in the array:
`=COUNTA(SORT(Q1:Q2))` and `=COUNTA(UNIQUE(Q1:Q2))` are 2 (SORT-021, UNIQUE-017). What the engine answers beyond
those, and why, none of it Excel's answer until its case is observed: `SORTBY`, `FILTER` and `TRANSPOSE` keep a blank
as `SORT` does, so theirs is taken to be the same blank, read as `SORT`'s is; without that, `=COUNTA(FILTER(...))`
would pass over a blank that `=COUNTA(SORT(FILTER(...)))` counts, though a reorder cannot change a count
(SORTBY-011 to 013, FILTER-012 and 013, TRANSPOSE-005 and 006 ask). A function that reads one Value reads the blank
`INDEX` takes out as a blank cell's, as `ISBLANK` was seen to, and an operator reads each Value so, as `=` was seen
to. `SUMPRODUCT` and `CONCAT` add nothing for it whichever way they read it, and `SUM` neither, except where it would
be the last number, which decides whether a sum that nearly cancels is 0 (SORT-033 and 037 ask). `VLOOKUP`, `HLOOKUP`
and `XLOOKUP` take an array that holds it where it is neither a key they search nor what they return. Where a
function's reading of it parts — `AVERAGE`, `MIN`, `MAX`, `COUNT` or `PRODUCT` passing over it or taking it as 0,
`COUNTA` given it as one Value, a search through it, `IF`, `CHOOSE`, `IFERROR` or `IFNA` given it, another of the
five — the Formula is `#VALUE!`. The refusal is the Formula's, not an Error Value the call returns, so `IFERROR`
cannot catch it and `COUNTA` cannot count it, as a Formula that meets `#CIRC!` is `#CIRC!` (ADR-0047); SORT-034, 035,
038, 039, UNIQUE-021 to 023, FILTER-014 and TRANSPOSE-007 ask. `LEN` is not yet lifted (above), so UNIQUE-019 stays
`#VALUE!` where Excel gives 2.)*
```

### 6b. A dated note on "Not yet: functions lifted over arrays"

Old:

```
this, and it comes as later tickets. Until a function is lifted, an array where it wants one Value
stays `#VALUE!`, as ADR-0047 had it: an Error Value, never another answer.
```

New:

```
this, and it comes as later tickets. Until a function is lifted, an array where it wants one Value
stays `#VALUE!`, as ADR-0047 had it: an Error Value, never another answer.

*(2026-10-12: **the refusal is the Formula's**, as ADR-0047 makes a Formula that meets `#CIRC!`
`#CIRC!`. A call that refused returned `#VALUE!` as an Error Value, and what was around it read it as
one: `=IFERROR(LEN(A1:A3),0)` was 0, `=ISERROR(ROUND(A1:A3,0))` TRUE and `=COUNTA(ROUND(A1:A3,0))` 1,
answers about a `#VALUE!` the engine made where Excel 365 lifts the function, though
`=IFERROR(A1:A2,0)` was already `#VALUE!` (IFERROR-009). Now nothing in the Formula reads the
refusal, given the range or around a call that was given it (IFERROR-017). A cell that reads such a
Formula's cell still reads `#VALUE!` as an Error Value.)*
```

## 7. ADR-0047: a dated note on the paragraph admitting the array functions

Old (under "The list grows", the paragraph that admits `FILTER` to `SUMPRODUCT` under ADR-0125):

```
answer, and their corpus cases changed from a difference by decision to it. `UNIQUE` refuses a blank
in its array, and `SORT` and `SORTBY` a blank or an Error Value among their keys, with `#VALUE!`
until Excel is asked how it orders them.
```

New:

```
answer, and their corpus cases changed from a difference by decision to it. `UNIQUE` refuses a blank
in its array, and `SORT` and `SORTBY` a blank or an Error Value among their keys, with `#VALUE!`
until Excel is asked how it orders them. *(2026-10-12, after the two area passes: the blank is
answered among `SORT`'s keys and in `UNIQUE`'s array as Excel answered it, among `SORTBY`'s as
Excel's was ascending and as `SORT`'s descending, and, read inside the Formula, where Excel's answers
or the agreement of every reading settle it; where a function's reading of it parts, the Formula is
`#VALUE!` (ADR-0125's note of that day). Over 7 and then `#N/A`, Excel's
`=SORT(A1:A2)` shows 7 (SORT-015): an Error Value key is neither `SORT`'s result nor sorted before a
number. The `#N/A` followed the 7 already, so the answer fits a key sorted after the numbers and one
that keeps its place alike. `SORT` answers a key only where every such reading gives the one order —
its Error Values all one, and beside them nothing, or, sorted ascending, numbers alone, none after an
Error Value — and the Formula is `#VALUE!` with a number after it, beside text, a boolean, a blank or
another Error Value, and in a descending sort (SORT-026, 028 to 032 ask). `SORTBY` refuses every Error
Value key: a `by_array`'s could as well be the result, as `FILTER`'s `include`'s is (SORTBY-010 asks).
Each refusal is the Formula's, as a Formula that meets `#CIRC!` is `#CIRC!`, so `IFERROR` and `COUNTA`
around it give no answer.)*
```

## 8. `src/ExSheet.Engine/README.md`

1. "Asked of Excel before they are answered": the fragment on `UNIQUE`, `SORT` and `SORTBY`. Other areas edit the same
   bullet, and the first line below begins with the financial area's fragment (`2^53 (`#VALUE!`);`): apply theirs
   first and change only this fragment.

   Old:

   ```
     2^53 (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank or an
     Error Value among their keys (`#VALUE!`; the seventeenth run read only the Anchor of a blank's
     sort).
   ```

   New:

   ```
     2^53 (`#VALUE!`); `SORT` with an Error Value key that has a number after it, or beside text, a
     boolean, a blank or another Error Value, or in a descending sort, and `SORTBY` with any Error
     Value key (`#VALUE!`); a blank `UNIQUE`, `SORT`, `SORTBY`, `FILTER` or `TRANSPOSE` keeps, read by
     a function whose readings of it part, such as `AVERAGE`, `MIN` or `COUNT` (`#VALUE!`). Each of
     these refusals is the Formula's, which `IFERROR` does not catch.
   ```

2. The paragraph on spilling, after "takes an array whole.".

   Old:

   ```
   spill. Operators work element by element over ranges, and a function that reduces (`SUM`,
   `SUMPRODUCT`) takes an array whole.
   ```

   New:

   ```
   spill. Operators work element by element over ranges, and a function that reduces (`SUM`,
   `SUMPRODUCT`) takes an array whole. A blank in a spilled array shows 0. Among `SORT`'s and
   `SORTBY`'s keys a blank sorts after every other key, in either order, blanks keeping their order,
   and in `UNIQUE` it is one of a kind only with another blank, never with 0. Read inside a Formula,
   the blank `UNIQUE`, `SORT`, `SORTBY`, `FILTER` and `TRANSPOSE` keep is read as the empty value a
   blank cell holds, as Excel's `ISBLANK`, `=` and `COUNTA` read `SORT`'s and `UNIQUE`'s, the other
   three taken to keep the same blank until Excel is asked: `COUNTA` counts it in the array, `INDEX`
   takes it out as a blank, and an operator or a function that reads one Value reads it as a blank
   cell's. `SUMPRODUCT` and `CONCAT` add nothing for it, `SUM` neither except
   where that moves its last addition, and `VLOOKUP`, `HLOOKUP` and `XLOOKUP` take an array that holds
   it where they never read it, each because every reading gives the one answer. A function that would
   read it some other way no run has settled makes the Formula `#VALUE!`. `SORT` answers an Error Value
   key that already follows every number, the one shape Excel was asked (SORT-015).
   ```

3. "Functions are not lifted over arrays yet".

   Old:

   ```
   - **Functions are not lifted over arrays yet** (ADR-0125). An array where a function wants one
     Value — `ROUND(A1:A3,0)`, `IF(A1:A3>1,…)` — is `#VALUE!`, where Excel 365 applies the function to
     each Value and spills. `IFERROR` and `ISERROR` do not turn that refusal into a fallback. The `@`
     operator is refused on entry.
   ```

   New:

   ```
   - **Functions are not lifted over arrays yet** (ADR-0125). An array where a function wants one
     Value — `ROUND(A1:A3,0)`, `IF(A1:A3>1,…)` — makes the Formula `#VALUE!`, where Excel 365 applies
     the function to each Value and spills. The refusal is the Formula's: `IFERROR`, `ISERROR`,
     `COUNTA` and the rest do not turn it into an answer, given the range or around a call that was
     given it, so `=IFERROR(LEN(A1:A3),0)` is `#VALUE!`. The `@` operator is refused on entry.
   ```

## 9. Found on the way, for the orchestrator

- **A refusal read from another cell is an ordinary Error Value.** The Formula's refusal holds inside one Formula. A
  cell whose Formula was refused holds `#VALUE!`, and a Formula that reads that cell can catch it: with B1
  `=LEN(A1:A3)`, `=IFERROR(B1,0)` is 0 and `=ISERROR(B1)` TRUE, where Excel's B1 spills Values. The same holds for
  every refusal of the engine, a kept blank's included; `SortAndUniqueBlankTests.ADR0125_a_formula_computed_on_demand_keeps_its_own_refusal`
  pins `ISERROR(INDIRECT("A6"))` TRUE over such a cell. Closing it needs a decision — a refused cell tainting the
  Formulas that read it, as a cycle taints them with `#CIRC!` (ADR-0047) — which is ADR-0047's and ADR-0125's, not
  this area's.
- **The engine's other refusals return a plain Error Value**, so the same hole stands for each of them within one
  Formula: `MOD`, `PV`, `FV`, `XNPV`, `SMALL`, `EOMONTH` and `EDATE`, `TEXT`, `XLOOKUP`'s binary search and the rest
  of ADR-0047's "admitted with some argument values refused". On this branch `=IFERROR(MOD(1,0.1),0)` is 0, where
  Excel's `MOD(1,0.1)` gave 0.09999999999999995 in the second area pass (MOD-020). Each is one call to
  `Evaluator.Refuse` where the function refuses, in other areas' code; ADR-0047's "every refused value gives an Error
  Value and never a different answer" reads as asking for it.
- **`IF`, `CHOOSE`, `IFS` and `SWITCH` refuse a kept blank they would only pass on.** Passing the operand on with its
  mark would answer wherever the reader after them answers, but whether Excel's `IF` hands an array's empty element on
  as it is, or as the 0 a Formula shows for it, no run has read. With `FILTER` marked, `=IF(C1,FILTER(A2:A9,B2:B9>0),0)`
  over a blank is now `#VALUE!`. SORT-039, `=ISBLANK(INDEX(IF(TRUE,SORT(Q1:Q2)),2))`, asks it: TRUE if `IF` hands the
  blank on as it is, FALSE if as 0.
- The engine has no `N` function, so it is not among the readers of a kept blank.

## 10. Docs Site: `samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`, "Supported functions"

The bullets under "Where the engine cannot give Excel's answer, it gives an Error Value instead of a different answer".

1. Old:

   ```
               <li>
                   <b>A function given a range where it takes one Value.</b> Excel 365 applies such a function to each
                   Value and spills the results, so <code>=ROUND(B2:E2, -3)</code> gives four. ExSheet does not yet: it is
                   <code>#VALUE!</code>.
               </li>
   ```

   New:

   ```
               <li>
                   <b>A function given a range where it takes one Value.</b> Excel 365 applies such a function to each
                   Value and spills the results, so <code>=ROUND(B2:E2, -3)</code> gives four. ExSheet does not yet: the
                   Formula is <code>#VALUE!</code>, and nothing around the call turns that into an answer, so
                   <code>=IFERROR(ROUND(B2:E2, -3), 0)</code> is <code>#VALUE!</code> too.
               </li>
   ```

2. Old:

   ```
               <li>
                   <b>A blank among <code>SORT</code>'s or <code>SORTBY</code>'s keys, or in <code>UNIQUE</code>'s
                   array,</b> is <code>#VALUE!</code>. Excel sorts it, but where it goes and what its cell shows have not
                   been read yet.
               </li>
   ```

   New:

   ```
               <li>
                   <b>An Error Value among <code>SORT</code>'s keys</b> is sorted where it already follows every number:
                   over 7 and then <code>#N/A</code>, Excel's Anchor shows 7, which fits an Error Value sorted after the
                   numbers and one that keeps its place alike. With a number after it, beside text, a boolean, a blank or
                   another Error Value, in a descending sort, and among <code>SORTBY</code>'s keys, the Formula is
                   <code>#VALUE!</code> until Excel has been asked where it goes.
               </li>
               <li>
                   <b>A blank among <code>SORT</code>'s or <code>SORTBY</code>'s keys</b> sorts last, in either order,
                   and in <code>UNIQUE</code> a blank is one of a kind only with another blank; it spills as 0, as
                   Excel's <code>SORT</code> and <code>UNIQUE</code> do.
               </li>
               <li>
                   <b>A blank <code>UNIQUE</code>, <code>SORT</code>, <code>SORTBY</code>, <code>FILTER</code> or
                   <code>TRANSPOSE</code> keeps, read inside a Formula.</b> <code>COUNTA</code> counts it and
                   <code>INDEX</code> takes it out as a blank, as Excel was seen to do for <code>SORT</code> and
                   <code>UNIQUE</code>; the other three are taken to keep the same blank until Excel has been asked. A
                   function whose readings of it part, such as <code>AVERAGE</code>, <code>MIN</code> or
                   <code>COUNT</code>, makes the Formula <code>#VALUE!</code>, which <code>IFERROR</code> does not catch,
                   so <code>=AVERAGE(FILTER(A2:A9, B2:B9="North"))</code> over a blank is <code>#VALUE!</code>.
               </li>
   ```

No Docs Site Example uses `SORT`, `SORTBY`, `UNIQUE`, `FILTER` or `TRANSPOSE`, or gives an unlifted function a range
inside `IFERROR` (`samples/` searched: `RegionalSales.cs`'s `=IFERROR(E10, 0)` reads a cell, and `OperationsBudget.cs`
divides two cells), so no Example is now false. `/exsheet/functions` needs nothing: it is generated from the engine's
function list, whose entries did not change. The site was not run: this branch does not edit it.

## 11. Change Notes

- `docs/releases/unreleased/area-sort-and-unique-blanks.md` (ExSheet, Behaviour, ADR-0125): rewritten; says which
  answers Excel was seen to give and which are the engine's.
- `docs/releases/unreleased/area-sort-and-unique-error-keys.md` (ExSheet, Behaviour, ADR-0047): rewritten; only an
  Error Value key that already follows every number is sorted.
- `docs/releases/unreleased/area-sort-and-unique-filter-transpose.md` (ExSheet, Behaviour, ADR-0125): new.
- `docs/releases/unreleased/area-sort-and-unique-unlifted-refusal.md` (ExSheet, Behaviour, ADR-0125): new.

All four link `exsheet/formulas#supported-functions`.

## 12. Nothing else

- `docs/definition-of-done.md`: SH-64 says "each to its corpus cases" and names no blank, so no edit.
- `CONTEXT.md`: no new term. "Kept blank" is the code's word for the blank the five keep, not the glossary's.
- No ticket under `docs/specs/` names these cases.
