# The area passes, sort and unique: what the orchestrator writes

Branch `claude/area2-sort-and-unique`, on `claude/area-corrections` (which holds the first round,
`claude/area-sort-and-unique`). The evidence: the first area pass (`verification/2026-10-12-windows-area/`,
`results-2026-10-12-area-com-en-US.json`) and the second (`verification/2026-10-12-windows-area-2/report.md`,
`results-2026-10-12-area-2-com-en-US.json`, Excel 16.0.20430.20146 under en-US through COM). This file replaces every
edit the first round's version of it proposed; none of those has been applied. It holds the text this area would put
into ADR-0125, ADR-0047, the engine's README and the Docs Site, which a background agent does not change. Each edit is
exact: the old lines, then the new, whole lines with the target's own indentation.

The branch edits itself: the engine, the corpus (`sort.json`, `sortby.json`, `unique.json`, and one new case each in
`transpose.json` and `filter.json`), `tests/ExSheet.Engine.Tests/SortAndUniqueBlankTests.cs`, the catalogue's `SORT`,
`SORTBY` and `UNIQUE` rows (`docs/specs/exsheet-functions/spec.md`), and the area's two Change Notes.

## 1. What Excel answered, and what the engine does now

The `lookupData` fixture has Q1 = 7 and Q2 blank.

| Case | Formula | Excel (second pass) | Before | Now |
|---|---|---|---|---|
| SORT-015 | `=SORT(A1:A2)` over 7 and `=NA()` | 7 | `#VALUE!` (refused) | 7, `observed` |
| SORT-021 | `=COUNTA(SORT(Q1:Q2))` | 2 | `#VALUE!` (refused) | 2, `observed` |
| SORT-022 | `=ISBLANK(INDEX(SORT(Q1:Q2),2))` | TRUE | `#VALUE!` (refused) | TRUE, `observed` |
| UNIQUE-017 | `=COUNTA(UNIQUE(Q1:Q2))` | 2 | `#VALUE!` (refused) | 2, `observed` |
| UNIQUE-018 | `=INDEX(UNIQUE(Q1:Q2),2)=""` | TRUE | `#VALUE!` (refused) | TRUE, `observed` |
| UNIQUE-019 | `=SUM(--(LEN(UNIQUE(A1:A3))>0))` over a, blank, b | 2 | `#VALUE!` (refused) | `#VALUE!`, `observed`, recorded with `excelExpect` 2 |

The 16 agreements `-Update` promoted (SORT-016 to 020, 023 to 025; SORTBY-007 to 009; UNIQUE-012 to 016) are unchanged
in what they expect; their descriptions no longer say "Engine's answer, Excel to be asked". Five older observed cases
said it too and no longer do: SORT-009, SORT-010, SORTBY-006, UNIQUE-007, UNIQUE-008.

### The blank read inside the Formula: the rule the answers settle

Three readings were open for a blank `UNIQUE`, `SORT` or `SORTBY` keeps from its array, read inside the Formula: the
0 it spills as, empty text, or the empty value a blank cell holds.

- **The 0 it spills as** is ruled out: `INDEX(UNIQUE(Q1:Q2),2)=""` is TRUE (UNIQUE-018), and 0 is not equal to empty
  text; `LEN` reads it as 0 (UNIQUE-019's 2 over a, blank, b is 1, 0, 1), where `LEN(0)` is 1; and `ISBLANK` of it is
  TRUE (SORT-022).
- **Empty text** is ruled out for `SORT` by `ISBLANK` (SORT-022), and for `UNIQUE` by its spill: an element of empty
  text spills as empty text, and UNIQUE-010 read 0.
- **The empty value a blank cell holds** fits all six answers. And `COUNTA` counts it in the array (SORT-021, UNIQUE-017:
  2 over 7 and a blank), where it passes over a blank cell of a range.

So: an operator reads each Value of such an array, and a function that reads one Value reads the blank `INDEX` takes
out, as it reads a blank cell's (that is what `ISBLANK`, `=""` and `LEN` showed); `COUNTA` counts it in the array.
`SORTBY` makes its array as `SORT` does, the array's lines reordered, so its blank is read as `SORT`'s (SORTBY-011 to
013 ask). A function that reads the array whole has a reading of its own, which these answers leave open: passing
over the blank as over a blank cell of a range, or reading it as the empty value of the kind it wants (0, "" or FALSE).
Where the two give the same answer for every array, the function takes it: `SUMPRODUCT` (anything but a number is 0),
`CONCAT` (nothing either way), and `SUM`, except where the blank would be its last number: the last addition decides
whether a sum that nearly cancels is 0 (ADR-0047, second run), so `SUM` computes both and refuses where they differ
(SORT-037). Where the two part, the Formula is refused, as before: `AVERAGE` (7 or 3.5 over 7 and a blank), `MIN`,
`MAX`, `COUNT`, `PRODUCT`, `AND`, `OR`, `TEXTJOIN` with `ignore_empty` FALSE, `COUNTA` given the blank alone as one
Value (counted as in the array, or not as a blank cell's), `MATCH`, `XLOOKUP`, another of the three, `FILTER` and
`TRANSPOSE`. `IF`, `CHOOSE`, `IFERROR` and `IFNA`, which pass a Value on unread, still refuse it too: passing it on
would agree with every reading, but `IFERROR` and `IFNA` give a blank back without the mark, so the reader after them
could not refuse, and no answer needs any of the four opened.

`LEN` and every function that wants one Value are not yet applied to each Value of an array (ADR-0125, "Not yet:
functions lifted over arrays"), so UNIQUE-019 stays `#VALUE!`, recorded with Excel's 2; given an array that holds a
kept blank, they refuse the Formula rather than return the `#VALUE!` an unlifted array gets, so that
`=IFERROR(LEN(UNIQUE(A1:A3)),0)` cannot be 0 (section 9).

### The Error Value key: the rule SORT-015 settles, and where it stops

Excel's `=SORT(A1:A2)` over 7 and `#N/A` shows 7. So an Error Value key is not `SORT`'s result, and does not sort
before the numbers. It leaves open where it sorts against text, booleans and blanks, whether a descending sort puts it
first (reversed with the other kinds) or still last (as a blank), and how two different Error Values order (kept in
order, or by `ERROR.TYPE`). A position-keeping reading (the Error Value's line stays where it is) is no order on the
keys, so it is not a candidate; SORT-026 asks the case that would rule it out anyway.

`SORT` answers a key where every candidate gives the same order: its Error Values are all one, and beside them it holds
nothing, or, sorted ascending, numbers alone. The line moves with its key; an Error Value outside the key column is
carried as any Value is (it was before). Everywhere else it is `#VALUE!`, and SORT-028 to 032 ask each place the
readings part. `SORTBY` refuses every Error Value key: SORT-015 read `SORT`'s, and a `by_array`'s Error Value could as
well be the result, as `FILTER`'s `include`'s is (SORTBY-010 asks).

No search: no digits decide in this area. The candidates are orders and readings, each listed above with the case
that rules it out or asks it.

## 2. The code

- `src/ExSheet.Engine/Formulas/Operand.cs`: `KeptBlank`, `HoldsBlank` and `WithKeptBlank()`: the operand holds a blank
  `UNIQUE`, `SORT` or `SORTBY` kept from its array, as one of its Values or as the one Value `INDEX` picked.
- `FunctionDefinition.cs`: `FunctionCall.Operand` refuses the Formula (`Evaluator.Refuse`) when the argument holds a
  kept blank; `KeepingBlanks` gives it to the readers whose reading is settled (`COUNTA`, `INDEX`, `SUM`,
  `SUMPRODUCT`, `CONCAT`); `OneValue` gives a kept blank Value to a function that reads one Value, and refuses an
  array holding one. `ValuesRead` is gone, and `Measured` evaluates the argument for its extent.
- `Evaluator.cs`: the syntactic position (`_unread`, `Unparenthesised`, `Measured`) is gone: the mark on the operand
  decides, so a `LET` name, parentheses and `INDEX` need no rule of their own. `Refuse` and the Formula's refusal stay.
- `Functions.Arrays.cs`: `Made` marks the result; `TryOrderLines` takes `errorKeysOrdered` (`SORT` true, `SORTBY`
  false) and `ErrorKeysSettled`; `SUMPRODUCT` reads through `KeepingBlanks`.
- `Functions.cs`: `COUNTA` counts every element of an array holding a kept blank, and refuses a kept blank given as one
  Value; `SUM` adds its numbers both ways (`AddNumbers`, split out of `CollectNumbers`) and refuses where the sums
  differ; `ISERROR` reads through `OneValue`. `Functions.Logic.cs`: the IS functions (`Inspect`) read through
  `OneValue`. `Functions.Arguments.cs`: `TryScalar` reads through `OneValue`. `Functions.Lookup.cs`: `INDEX` reads its
  array through `KeepingBlanks` and marks what it picks. `Functions.Text.cs`: `CONCAT` through `KeepingBlanks`.

## 3. Tests and counts

`SortAndUniqueBlankTests.cs` (each test named with ADR-0125, or ADR-0047 for the Error Value key): the Error Value key
answered (5 rows) and refused (9 rows), its line moving with it, an Error Value outside the key; `COUNTA` counting a
kept blank (7 rows); one-Value readers of the blank `INDEX` takes out (16 rows); operators (3 rows) and `&` spilling
empty text; `SUM`, `SUMPRODUCT` and `CONCAT` taking it (6 rows); `SUM` refusing where its last addition moves and
answering where it does not (6 rows);
the readings that part refused (20 rows), and not caught (6 rows); the verifier's contradiction (`COUNTA(D1#)` 3 and
`COUNTA(UNIQUE(A1:A3))` 3) now agreeing; the spill under parentheses, `LET` and `INDEX` with a column of 0; results
without a blank; calls never evaluated; a Formula computed on demand keeping its refusal.

With the refusal in `FunctionCall.Operand` taken out and `ErrorKeysSettled` made to accept every key, 35 tests and
corpus cases fail.

- `tests/ExSheet.Engine.Tests`, the whole project: **5266 passed, 0 failed, 3 skipped** (5184 passed before, on
  `caa73261`: 23 new corpus cases, and `SortAndUniqueBlankTests` from 72 tests to 131).
- `tests/ExSheet.Components`: **752 passed, 0 failed**.

## 4. New cases (`uncertain`, the engine's answer as `expect`)

| Case | Formula | The engine | What it asks |
|---|---|---|---|
| SORT-026 | `=SORT(A1:A2)` over `=NA()`, 7 | 7 | an Error Value key before a number moves after it (#N/A if it kept its place) |
| SORT-027 | `=SORT(A1:A2)` over 7, `=NA()`, at ZZ1001 | `#N/A` | the Error Value key's own cell shows it |
| SORT-028 | `=SORT(A1:A2,1,-1)` over 7, `=NA()` | `#VALUE!` | descending: 7 if the Error Value stays last, `#N/A` if it comes first |
| SORT-029 | `=SORT(A1:A2)` over a, `=NA()` | `#VALUE!` | a if it sorts after text, `#N/A` if before |
| SORT-030 | `=SORT(A1:A2)` over TRUE, `=NA()` | `#VALUE!` | TRUE if it sorts after booleans, `#N/A` if before |
| SORT-031 | `=SORT(A1:A2)` over `=NA()`, blank | `#VALUE!` | `#N/A` if it sorts before a blank, 0 if after |
| SORT-032 | `=INDEX(SORT(A1:A3),2)` over 7, `=NA()`, `=1/0` | `#VALUE!` | `#N/A` if two Error Values keep their order, `#DIV/0!` if `ERROR.TYPE` orders them |
| SORT-033 | `=SUM(SORT(Q1:Q2))` | 7 | `SUM` adds nothing for the kept blank |
| SORT-034 | `=COUNT(SORT(Q1:Q2))` | `#VALUE!` | 1 if it is no number to `COUNT`, 2 if read as 0 |
| SORT-035 | `=COUNTA(INDEX(SORT(Q1:Q2),2))` | `#VALUE!` | 1 if `COUNTA` counts the blank given alone as it counts it in the array, 0 if as a blank cell's |
| SORT-036 | `=INDEX(SORT(Q1:Q2),2)` | 0 | a Formula whose result is the kept blank shows 0, as one reading a blank cell does |
| SORT-037 | `=SUM(SORT(A1:A4))` over 0.1, 0.2, -0.3, blank | `#VALUE!` | 0 if `SUM` passes over the blank (its last addition, of 0.2, cancels), 2.78E-17 if it adds it as 0 last |
| SORTBY-010 | `=SORTBY(H1:H2,Z1:Z2)`, Z1 7, Z2 `=NA()` | `#VALUE!` | 10 if `SORTBY` sorts the Error Value as `SORT` does, `#N/A` if it is the result |
| SORTBY-011 | `=COUNTA(SORTBY(Q1:Q2,H1:H2))` | 2 | `SORTBY`'s kept blank is counted as `SORT`'s |
| SORTBY-012 | `=ISBLANK(INDEX(SORTBY(Q1:Q2,H1:H2,-1),1))` | TRUE | and is blank to `ISBLANK` |
| SORTBY-013 | `=SORTBY(Q1:Q2,H1:H2,-1)` | 0 | a blank in `SORTBY`'s array spills as 0 |
| UNIQUE-020 | `=ISBLANK(INDEX(UNIQUE(Q1:Q2),2))` | TRUE | `UNIQUE`'s kept blank is blank to `ISBLANK`, as `SORT`'s is |
| UNIQUE-021 | `=AVERAGE(UNIQUE(Q1:Q2))` | `#VALUE!` | 7 if passed over, 3.5 if read as 0 |
| UNIQUE-022 | `=MIN(UNIQUE(Q1:Q2))` | `#VALUE!` | 7 or 0 |
| UNIQUE-023 | `=COUNTA(UNIQUE(Q2))` | `#VALUE!` | 1 if counted as in a longer array, 0 as a blank cell's |
| UNIQUE-024 | `=UNIQUE(A1:A3)&""` over a, blank, b, at ZZ1001 | empty text | `&` reads the kept blank as empty text |
| TRANSPOSE-005 | `=COUNTA(TRANSPOSE(Q1:Q2))` | 1 | section 9: 2 if `TRANSPOSE` keeps the blank as `SORT` does |
| FILTER-012 | `=COUNTA(FILTER(Q1:Q2,H1:H2>0))` | 1 | section 9: the same for `FILTER` |

The next `-Area` pass names `sort`, `sortby`, `unique`, `transpose` and `filter`.

## 5. What remains unsettled, and why

- **Where an Error Value key sorts** against text, booleans and blanks, in a descending sort, and against another Error
  Value (SORT-028 to 032), and whether `SORTBY` sorts one at all (SORTBY-010). SORT-015 is one ascending pair.
- **How a function that reads an array whole reads a kept blank**, where passing over it and reading it as an empty
  value part: `AVERAGE`, `MIN`, `MAX`, `COUNT`, `PRODUCT`, `AND`, `OR`, `TEXTJOIN`, lookups, and `SUM` where the last
  addition moves (SORT-034, 037, UNIQUE-021, 022 ask some). `COUNTA` given the blank alone (SORT-035, UNIQUE-023).
- **`LEN` and the functions not yet applied to each Value of an array**: UNIQUE-019 stays recorded until ADR-0125's
  lifting is built.
- **`SORTBY`'s and `UNIQUE`'s kept blank inside a Formula** are answered by `SORT`'s rule, which no run has read for
  them directly beyond UNIQUE-017 and 018 (SORTBY-011 to 013, UNIQUE-020 ask).

## 6. ADR-0125: a dated note after the note of 2026-10-12

Old (the end of the note of 2026-10-12, "What becomes Excel's answer"):

```
guess where the blank goes, what its spilled cell shows, how a descending sort places it and whether `UNIQUE` counts
it one of a kind with 0; SORT-012 to 014 and UNIQUE-010 and 011 ask those.)*
```

New:

```
guess where the blank goes, what its spilled cell shows, how a descending sort places it and whether `UNIQUE` counts
it one of a kind with 0; SORT-012 to 014 and UNIQUE-010 and 011 ask those.)*

*(2026-10-12, after the two area passes, `verification/2026-10-12-windows-area/` and `verification/2026-10-12-windows-area-2/`:
**a blank among `SORT`'s and `SORTBY`'s keys, and in `UNIQUE`'s array, is answered as Excel answers it, and so is the
blank the three keep, read inside the Formula, where the answers settle how.** The refusal of the note above is
withdrawn. Over 7 and a blank, `=SORT(Q1:Q2)` spills 7 and then 0, ascending or descending, and over b, a blank and a,
`=SORT(A1:A3)` is a first: a blank key sorts after every other, in either order, neither as 0 nor as empty text, and
blank keys keep their order (SORT-011 to 014, 016 to 020, 023 to 025). `SORTBY` sorts its blank keys so, key by key
(SORTBY-007 to 009). In `UNIQUE` a blank is one of a kind only with another blank, never with 0 or empty text
(UNIQUE-011 to 015). A blank spills as 0 (SORT-012, UNIQUE-010, 016). Read inside the Formula, the blank they keep is
the empty value a blank cell holds: `=ISBLANK(INDEX(SORT(Q1:Q2),2))` is TRUE (SORT-022), `=INDEX(UNIQUE(Q1:Q2),2)=""`
TRUE (UNIQUE-018), and `LEN` reads it as 0 (UNIQUE-019): not the 0 it spills as, which would not equal empty text, nor
empty text, which would spill as itself. `COUNTA` counts it in the array: `=COUNTA(SORT(Q1:Q2))` and
`=COUNTA(UNIQUE(Q1:Q2))` are 2 (SORT-021, UNIQUE-017). So an operator reads each Value of such an array, and a function
that reads one Value reads the blank `INDEX` takes out, as it reads a blank cell's; `COUNTA` counts it; `SUMPRODUCT` and
`CONCAT` add nothing for it, whichever way they read it, and `SUM` neither, except where it would be the last number,
which decides whether a sum that nearly cancels is 0 (SORT-037 asks). Where a function's reading of it parts —
`AVERAGE`, `MIN`, `MAX`, `COUNT` or `PRODUCT` passing over it or taking it as 0, `COUNTA` given it as one Value, a
lookup, another of the three — the Formula is `#VALUE!`. The refusal is the Formula's, not an Error Value the call
returns, so `IFERROR` cannot catch it and `COUNTA` cannot count it, as a Formula that meets `#CIRC!` is `#CIRC!`
(ADR-0047); SORT-034, 035 and UNIQUE-021 to 023 ask. `LEN` is not yet lifted (above), so
`=SUM(--(LEN(UNIQUE(A1:A3))>0))` stays `#VALUE!` where Excel gives 2 (UNIQUE-019). `SORTBY`'s kept blank is read as
`SORT`'s (SORTBY-011 to 013 ask). `FILTER` and `TRANSPOSE` keep a blank too, and a Formula reads it there as a blank
cell, which `COUNTA` passes over: whether Excel counts theirs as it counts `SORT`'s is asked (TRANSPOSE-005,
FILTER-012).)*
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
answered as Excel answers it, among the keys, in `UNIQUE`'s array and, where the answers settle how,
read inside the Formula; where a function's reading of it parts, the Formula is `#VALUE!`
(ADR-0125's note of that day). Over 7 and `#N/A`, Excel's `=SORT(A1:A2)` shows 7 (SORT-015): an
Error Value key is neither `SORT`'s result nor first. So `SORT` puts it after the numbers where that
is all the order needs — its Error Values all one, and beside them nothing, or, sorted ascending,
numbers alone — and refuses it with `#VALUE!` beside text, a boolean, a blank or another Error Value,
and in a descending sort, where the readings SORT-015 leaves part (SORT-028 to 032 ask). `SORTBY`
refuses every Error Value key: a `by_array`'s could as well be the result, as `FILTER`'s `include`'s
is (SORTBY-010 asks).)*
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
     2^53 (`#VALUE!`); `SORT` with an Error Value key beside text, a boolean, a blank or another Error
     Value, or in a descending sort, and `SORTBY` with any Error Value key (`#VALUE!`); a blank
     `UNIQUE`, `SORT` or `SORTBY` keeps, read by a function whose reading of it no run has settled,
     such as `AVERAGE`, `MIN` or `COUNT` (`#VALUE!`, which `IFERROR` does not catch).
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
   and in `UNIQUE` it is one of a kind only with another blank, never with 0, as Excel's are. Read
   inside a Formula, the blank they keep is the empty value a blank cell holds, as Excel's is:
   `COUNTA` counts it in the array, `INDEX` takes it out as a blank, and an operator or a function
   that reads one Value reads it as a blank cell's; `SUMPRODUCT` and `CONCAT` add nothing for it,
   and `SUM` neither, except where that moves its last addition. A function that would read it some
   other way no run has settled makes the Formula `#VALUE!`. `SORT` puts an Error Value key after
   the numbers, as Excel does, where that is all the order needs.
   ```

## 9. Found on the way, for the orchestrator

- **`FILTER` and `TRANSPOSE` may give a plausible wrong count.** Both keep a blank in their array, unmarked, and a
  Formula reads it as a blank cell: `=COUNTA(TRANSPOSE(Q1:Q2))` and `=COUNTA(FILTER(Q1:Q2,H1:H2>0))` are 1. Excel's
  `COUNTA` counts the blank `SORT` and `UNIQUE` keep (SORT-021, UNIQUE-017); if `TRANSPOSE` and `FILTER` keep it as
  they do, Excel's answer is 2. This branch left them as they are (the brief named `SORT`, `SORTBY` and `UNIQUE`) and
  added TRANSPOSE-005 and FILTER-012 to ask. Marking their result as `Made` marks `SORT`'s is one line each
  (`Operand.Of(result).WithKeptBlank()`), and would refuse them where `SORT`'s are refused until the answers come;
  `SUM(FILTER(...))` over a blank would then refuse only where its last addition moves.
- **`IFERROR` catches the refusal of an unlifted function nested inside it.** `=IFERROR(LEN(A1:A3),0)` is 0, and so is
  `=IFERROR(ROUND(A1:A3,0),0)`: the inner function returns `#VALUE!` for an array it is not yet applied to (ADR-0125),
  and `IFERROR` reads that as an error to catch, where Excel spills the function applied to each Value. The README says
  `IFERROR` and `ISERROR` do not turn that refusal into a fallback; they do not when given the range itself, and do
  when it is nested. This branch keeps the hole closed where an array holds a kept blank (`OneValue`), and does not
  touch the general case, which is ADR-0125's and the engine's, not this area's.
- `transpose.json`'s TRANSPOSE-004 is `observed` and still says "Engine's answer, Excel to be asked"; not changed here.

## 10. Docs Site: `samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`, "Supported functions"

The bullet under "Where the engine cannot give Excel's answer, it gives an Error Value instead of a different answer".

Old:

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
                <b>An Error Value among <code>SORT</code>'s keys</b> sorts after the numbers, as in Excel; beside
                text, a boolean, a blank or another Error Value, or in a descending sort, it is <code>#VALUE!</code>
                until Excel has been asked where it goes, and so is any Error Value among <code>SORTBY</code>'s keys.
                A blank among the keys sorts last, in either order, and shows 0, as in Excel; in <code>UNIQUE</code> a
                blank is one of a kind only with another blank.
            </li>
            <li>
                <b>A blank <code>UNIQUE</code>, <code>SORT</code> or <code>SORTBY</code> keeps, read inside a
                Formula,</b> is read as Excel reads it: <code>COUNTA</code> counts it, so
                <code>=COUNTA(UNIQUE(A2:A9))</code> counts a blank among the values as one, and <code>INDEX</code>
                takes it out as a blank. A function Excel has not been asked about, such as <code>AVERAGE</code>,
                <code>MIN</code> or <code>COUNT</code>, makes the Formula <code>#VALUE!</code>, which
                <code>IFERROR</code> does not catch.
            </li>
```

No Docs Site Example uses `SORT`, `SORTBY` or `UNIQUE` (`samples/` searched), so no Example is now false.
`/exsheet/functions` needs nothing: it is generated from the engine's function list, whose entries did not change.
The site was not run: this branch does not edit it.

## 11. Change Notes

- `docs/releases/unreleased/area-sort-and-unique-blanks.md` (ExSheet, Behaviour, `exsheet/formulas#supported-functions`,
  ADR-0125): rewritten to say what is true now, the blank read inside a Formula included.
- `docs/releases/unreleased/area-sort-and-unique-error-keys.md` (ExSheet, Behaviour, the same page, ADR-0047): new.

## 12. Nothing else

- `docs/definition-of-done.md`: SH-64 says "each to its corpus cases" and names no blank, so no edit.
- `CONTEXT.md`: no new term. "Kept blank" is the code's word for the blank the three keep, not the glossary's.
- No ticket under `docs/specs/` names these cases.
