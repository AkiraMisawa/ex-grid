# Run 17 corrections: SUBTOTAL, AGGREGATE, LARGE, MOD, RANK.EQ — proposals for the orchestrator

Branch `claude/run17-subtotal-aggregate`. The engine, the corpus, the tests, ticket 16 and the Change Notes
are on the branch. What follows is the text this area would put into files a background agent does not edit:
ADR-0047, the Definition of Done, the engine's README, the function catalogue (`spec.md`) and the Docs
Site. Each block is written to be pasted; the sentence it replaces is quoted where there is one.

Evidence: E1 = `tests/ExSheet.Engine.Tests/ExcelOracle/results-2026-10-11-run17-update-en-US.json`,
E2 = `…-run17-update-revision-en-US.json`, E3 = `…-run17-update-aggregate-new.json`, and
`verification/2026-10-11-windows-17/report.md` (Part A's "Every disagreement", Part B's b4).

---

## 1. ADR-0047: a dated note

To add after the bullet "**`SUBTOTAL` and `AGGREGATE` are admitted while a Sheet cannot hide a row.**" of
"The catalogue's last Decide rows" (its third sub-bullet, "Any other call of either function inside the
range makes the result `#VALUE!` until a Windows run asks Excel", is what the note replaces):

> *(2026-10-12)* **What the seventeenth Windows run read, and the engine now follows.** As this note and
> SH-68's said it would once a run had read Excel's answer:
>
> - **A cell is nested when its Formula calls `SUBTOTAL` or `AGGREGATE` anywhere**, or, for a cell of a
>   Spill Range, when its Anchor's does: the whole Formula, inside a larger expression, in parentheses,
>   under `IF`, plus 0 (SUBTOTAL-020, 021, 035 to 038; AGGREGATE-026, 052 to 054). `SUBTOTAL`, whatever its
>   function number, and `AGGREGATE`'s options 0 to 3 pass over it. Options 4 to 7 count its Value, in the
>   reference form and the array form alike (AGGREGATE-025, 042 to 051), as Microsoft documents. No
>   `#VALUE!` for nesting remains. The rule reads the Formula's text: a call in a branch `IF` never takes
>   is nested too, which SUBTOTAL-039 asks Excel.
> - **A function number that is not whole is truncated toward zero**, and the range checked after:
>   `SUBTOTAL(9.5,…)` is `SUBTOTAL(9,…)` (SUBTOTAL-027). `AGGREGATE`'s function number and option are
>   truncated the same way, as LibreOffice reads both; AGGREGATE-055 and 056 ask Excel.
> - **An argument Excel takes only as a Reference refuses, on entry, what can never be one.** Excel would
>   not enter `SUBTOTAL(9,SEQUENCE(3))`, `SUBTOTAL(9,5)`, `RANK.EQ(1,1)` or `AGGREGATE(14,6,A1:A11,1,2)`,
>   and entered `AGGREGATE(9,6,A1:A11*1)`, `AGGREGATE(14,6,5,1)` and a number as `AGGREGATE`'s fourth
>   argument. So `SUBTOTAL`'s references, `RANK.EQ`'s `ref`, and `AGGREGATE`'s fifth and later arguments
>   refuse a constant, an operator's result (parentheses around a Reference excepted) and a call of a
>   function that never gives a Reference, as an argument count is refused. A call of `INDIRECT`,
>   `OFFSET`, `INDEX`, `CHOOSE`, `IF`, `IFS`, `SWITCH`, `XLOOKUP` or `LET`, a name, an Error Value (where a
>   deletion left `#REF!`) and an argument left empty are taken, and judged when computed. A Sheet
>   Document holding a Formula so refused is refused when it is read, and pasted such a Formula is text,
>   as one that cannot be read is (ADR-0048). The other functions' reference arguments (`SUMIF`'s
>   `range`, `COUNTBLANK`'s, `OFFSET`'s `reference`, `ROW`'s and `COLUMN`'s) have not been asked, and keep
>   their `#VALUE!` until they are.
> - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
>   (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
>   and stay `#VALUE!` (AGGREGATE-061).
> - **`LARGE`'s k that is not whole is rounded up**: `LARGE(A1:B5,1.5)` and `AGGREGATE(14,6,A1:A11,1.5)`
>   are each the 2nd largest (LARGE-007, AGGREGATE-038), which rules out truncation; ODF 1.3's `LARGE`
>   (6.18.40), LibreOffice and Gnumeric round it up, and rounding to nearest cannot be told from it at .5
>   (LARGE-008 and AGGREGATE-059 ask 1.2). `SMALL`'s k that is not whole, where LibreOffice and ODF round
>   down and Gnumeric up, and a k between 0 and 1, where they disagree too, stay `#VALUE!` until Excel is
>   asked (SMALL-004, 005, AGGREGATE-060, LARGE-011). k is read at 15 significant digits first, as both
>   implementations do, so `1+2^-52` is 1 (LARGE-012, SMALL-006).
>
> The `MOD` refusal of "admitted with some argument values refused" above is replaced: Excel 365 answers
> `MOD(2^27,1)` with 0 (MOD-008). Microsoft documents no limit, and names
> `MOD(n, d) = n - d*INT(n/d)`, which the engine computes in doubles and which gives Excel's digits on every
> case read (MOD-009's 0.09999999999999998 among them). A Microsoft 365 user reported on Microsoft's Q&A
> (2021) that `MOD(1125899999999,1)` is 0 and `MOD(1125900000000,1)` is `#NUM!`, a limit Excel 2010 brought
> in; the engine answers a quotient below 1,125,900,000,000 and is `#NUM!` from it, and MOD-010 to MOD-017
> ask Excel on both sides of it.

In the paragraph "Following "admitted with some argument values refused", three are refused until Excel
answers: a `MOD` whose quotient is 2^27 or more (`#NUM!`, the answer older versions of Excel were known to
give); …", a pointer after "give)": *"(answered by the seventeenth Windows run: see the note of 2026-10-12
under "The catalogue's last Decide rows")"*.

## 2. Definition of Done: SH-68

Replace the sentence "Inside their range, only a cell whose whole Formula is a `SUBTOTAL` (and, for
`AGGREGATE`'s options 0 to 3, one whose whole Formula is an `AGGREGATE`) is skipped as nested; any other
call of either makes the result `#VALUE!`, as does an `AGGREGATE` function number for a function the engine
does not support." with:

> Inside their range, a cell whose Formula, or whose Spill Range's Anchor's, calls `SUBTOTAL` or
> `AGGREGATE` anywhere is nested: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it and options 4 to
> 7 count its Value, as the seventeenth Windows run read Excel's answers; an `AGGREGATE` function number
> for a function the engine does not support is `#VALUE!`, and a Formula that gives one of their
> reference-only arguments something that can never be a Reference is refused on entry.

and append to the dated italic note: *"(2026-10-12: the seventeenth run read it — 6, counted — and the
engine follows Excel, ADR-0047's note of that date.)"*

## 3. The engine's README (`src/ExSheet.Engine/README.md`)

- In "**Asked of Excel before they are answered**", remove "`MOD` whose quotient is 2^27 or more (`#NUM!`,
  as older versions of Excel gave);" and replace "`LARGE` and `SMALL` with a `k` that is not a whole
  number;" with "`SMALL` with a `k` that is not a whole number, and `LARGE` with one between 0 and 1;".
- A bullet to add beside it:

  > - **Answered as the seventeenth Windows run read Excel.** `MOD` answers a quotient of 2^27 or more, and
  >   is `#NUM!` from 1,125,900,000,000, the limit a Microsoft 365 user reported (MOD-010 to 017 ask
  >   Excel). `LARGE`, and `AGGREGATE`'s 14, round a `k` that is not whole up: `LARGE(A1:B5,1.5)` is the
  >   2nd largest. `k` is read at 15 significant digits. `RANK.EQ`'s `ref`, `SUBTOTAL`'s references and
  >   `AGGREGATE`'s fifth and later arguments refuse on entry what can never be a Reference — a constant,
  >   an operator's result, a call such as `SEQUENCE(3)` — as Excel does.

- Replace the `SUBTOTAL` and `AGGREGATE` bullet's sentences from "Microsoft documents that both pass over
  nested subtotals" to "(`PERCENTILE` and `QUARTILE`)." with:

  > A cell is nested when its Formula, or its Spill Range's Anchor's, calls `SUBTOTAL` or `AGGREGATE`
  > anywhere, as the seventeenth Windows run read Excel's answers: `SUBTOTAL` and `AGGREGATE`'s options 0
  > to 3 pass over it, and options 4 to 7 count its Value. A function number or option that is not whole
  > is truncated (`SUBTOTAL(9.5,…)` is `SUBTOTAL(9,…)`), and `AGGREGATE`'s array form takes a number given
  > directly. `#VALUE!`: an argument that is no reference when it is computed, and `AGGREGATE`'s function
  > numbers for functions the engine does not support: 13 (`MODE.SNGL`) and 16 to 19 (`PERCENTILE` and
  > `QUARTILE`), where Excel answers (AGGREGATE-027 to 031 record its answers).

## 4. The function catalogue (`docs/specs/exsheet-functions/spec.md`)

- `SUBTOTAL`'s Notes: "Admitted while a Sheet cannot hide a row, behind a test that fails when one can; a
  cell whose Formula calls `SUBTOTAL` or `AGGREGATE` anywhere is nested and passed over, as Excel does
  (ADR-0047, 2026-10-10 and 2026-10-12). Ticket 16"
- `AGGREGATE`'s Notes: "As `SUBTOTAL`; options 0 to 3 pass over a nested cell and 4 to 7 count it, and a
  function number for a function the engine does not support (13, 16 to 19) is `#VALUE!`. Ticket 16"
- `LARGE`: "A `k` that is not whole is rounded up, as Excel's (ADR-0047, 2026-10-12)"; `SMALL`: "A `k` that
  is not whole is `#VALUE!` until observed"; `MOD`: "Excel's sign follows the divisor; `#NUM!` from a
  quotient of 1,125,900,000,000"; `RANK.EQ`: "A `ref` that can never be a Reference is refused on entry".

## 5. The Docs Site

### `/exsheet/formulas`, "Supported functions" (`SheetFormulas.razor`)

Replace the `SUBTOTAL` and `AGGREGATE` list item with:

> **`SUBTOTAL` and `AGGREGATE`** answer while a Sheet cannot hide a row, so `SUBTOTAL`'s 101 to 111 give
> what 1 to 11 give, as Excel's do when nothing is hidden. A cell whose Formula calls either anywhere —
> inside a larger expression, under `IF`, or in the Anchor of the Spill Range it belongs to — is nested, as
> Excel counts one: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it, and options 4 to 7 count its
> value. An `AGGREGATE` function number for a function the Sheet does not have, such as 13,
> `MODE.SNGL`, is `#VALUE!`.

and add two list items after it:

> **Arguments that take only a Reference.** `SUBTOTAL`'s references, `RANK.EQ`'s `ref` and `AGGREGATE`'s
> fifth and later arguments refuse, as the Formula is typed, what can never be a Reference — a number, a
> text, an operator's result such as `A1:A3*1`, a call such as `SEQUENCE(3)` — as Excel refuses
> `=SUBTOTAL(9,5)`. `INDIRECT`, `OFFSET`, `INDEX`, `CHOOSE`, `IF` and their kin are taken, since they can
> give one.

> **`LARGE` and `SMALL`'s k.** `LARGE` rounds a k that is not whole up, as Excel does: `=LARGE(A1:B5,1.5)` is
> the 2nd largest. `SMALL`'s, and a k between 0 and 1, are `#VALUE!` until Excel has been asked.
> **`MOD`** is `#NUM!` from a quotient of 1,125,900,000,000, the limit reported of Excel 365.

### `/exsheet/functions`

No sentence: the page lists each function's signature and description from `DeclaredFunction.All`, which
this change leaves as they were. **Found while writing the Change Notes:** the Docs build refuses
`Docs: exsheet/functions#aggregation` (EXDOCS009, "The page /exsheet/functions has no section
#aggregation. Its sections are ."): the generator scans `DocsSection`s, and the page's category sections are
rendered by `FunctionTable` from the catalogue. The notes link `exsheet/formulas#supported-functions`, where
the prose lives. If notes should reach a category, the generator could add `FunctionCatalogue`'s category
anchors to the route's sections (a Docs generator change, not this area's).

## 6. Research: what reproduces Excel's digits

### MOD (every observed `MOD` value: MOD-001 to 009 in E1; MOD-005 is `#DIV/0!` and left out)

| Candidate | Matches |
|---|---|
| `n - d*floor(n/d)` in doubles (the engine's, Microsoft's definition) | **8 / 8** |
| C's `fmod`, the sign moved to the divisor's | 8 / 8 |
| `n - d*INT(n/d)` with `n/d` first rounded to 15 significant digits | 7 / 8 (MOD-009: -5.55E-17) |
| the remainder of the typed decimals, exactly | 7 / 8 (MOD-009: 0) |
| the first, its result rounded to 15 significant digits | 7 / 8 (MOD-009: 0.1) |

The first two agree on every observed case. MOD-017 tells them apart below the limit:
`MOD(123456789012,0.7)` is 0.5000152587890625 by the first and 0.5000078322611261 by `fmod`. The engine keeps
the first, Microsoft's own definition.

The limit: Microsoft's MOD page documents none; "What's New: Changes made to Excel functions" says Excel
2010's MOD "uses new algorithms to achieve both accuracy and speed"; Microsoft Q&A "Excel MOD function is
broken (#NUM error)" (2021, Microsoft 365) shows `=MOD(1125899999999,1)` 0 and `=MOD(1125900000000,1)`
`#NUM!`, and its answer says the limit has been `n >= d*1125900000000` since Excel 2010, after `n >= d*2^27`
in Excel 2003 and earlier. The engine refuses from that quotient, which lies just above 2^40
(1,099,511,627,776); MOD-010 asks 2^40 itself.

### k (every observed `LARGE`, `SMALL` and `AGGREGATE` 14/15 value: LARGE-001 to 007, SMALL-001 to 003, AGGREGATE-002, 003, 016, 020, 037 to 039, 049 to 051)

| Candidate for a k that is not whole | LARGE-007, AGGREGATE-038 | The rest (whole k) |
|---|---|---|
| truncate | 0 / 2 (7 and 96) | all |
| round up (ceiling) | **2 / 2** | all |
| round half up | 2 / 2 | all |
| round half to even | 2 / 2 | all |
| ODF 1.3 / LibreOffice: `LARGE` up, `SMALL` down | 2 / 2 | all |
| Gnumeric: up for both, `#NUM!` below 1 | 2 / 2 | all |

No observed value has a fractional k for `SMALL`. The sources: ODF 1.3 part 4, 6.18.40 `LARGE`
("Constraints: ROUNDUP(N;0) = N") and 6.18.70 `SMALL` ("ROUNDDOWN(N;0) = N"); LibreOffice
`sc/source/core/tool/interpr3.cxx`, `CalculateSmallLarge` (`approxFloor` for `SMALL`, `approxCeil` for
`LARGE`); Gnumeric `plugins/fn-stat/functions.c`, `gnm_kth` (`gnm_fake_ceil` for both, `gnm_fake_floor` below
1). All agree on `LARGE` from 1 up, which the engine implements; they disagree on `SMALL` and below 1, which
stay refused.

### Function numbers

`SUBTOTAL(9.5,…)` 303 rules out rounding half up and half to even (both 10); truncation and flooring agree
on every number in range. LibreOffice's `ScSubTotal` and `ScAggregate` read both arguments with
`GetInt32`, which truncates.

## 7. New `uncertain` cases: an `oracle.ps1 -Area` run before the tag (ADR-0208)

`-Area subtotal,aggregate,large,small,mod`:

| Case | Formula | Engine | Tells |
|---|---|---|---|
| SUBTOTAL-039 | A3 `=IF(FALSE,SUBTOTAL(9,A1:A2),5)`; `=SUBTOTAL(9,A1:A3)` | 3 | nested by text (3) or by evaluation (8) |
| SUBTOTAL-040 | `=SUBTOTAL(9,"A2:A5")` | refused | a text constant |
| SUBTOTAL-041 | `=SUBTOTAL(9,A2:A5*1)` | refused | an operator's result |
| SUBTOTAL-042 | `=SUBTOTAL(9,-A2:A5)` | refused | a unary operator |
| SUBTOTAL-043 | `=SUBTOTAL(9,IF(TRUE,A2:A5))` | 303 | IF taken |
| SUBTOTAL-044 | `=SUBTOTAL(9,(A2:A5))` | 303 | parentheses around a Reference |
| AGGREGATE-055 | `=AGGREGATE(9.5,6,A1:A11)` | 462 | function number truncated |
| AGGREGATE-056 | `=AGGREGATE(9,6.5,A1:A11)` | 462 | option truncated |
| AGGREGATE-057 | `=AGGREGATE(14,6,A1:A11,1,B1:B3)` | `#VALUE!` | AGGREGATE-040's refusal is by the fifth argument's class (taken here), not the array form's count (refused) |
| AGGREGATE-058 | `=AGGREGATE(9,6,A1:A3,B1:B3,5)` | refused | the same in the reference form |
| AGGREGATE-059 | `=AGGREGATE(14,6,A1:A11,1.2)` | 81 | ceiling (81) or rounding (96) |
| AGGREGATE-060 | `=AGGREGATE(15,6,A1:A11,1.2)` | `#VALUE!` | SMALL's k: 30 down, 31 up |
| AGGREGATE-061 | `=AGGREGATE(14,6,"5",1)` | `#VALUE!` | text given directly |
| LARGE-008 | `=LARGE(A1:B5,1.2)` | 6 | ceiling (6) or rounding (7) |
| LARGE-009 | `=LARGE(A1:B5,9.5)` | 2 | rounded up to the count |
| LARGE-010 | `=LARGE(A1:B5,10.5)` | `#NUM!` | rounded up past it |
| LARGE-011 | `=LARGE(A1:B5,0.5)` | `#VALUE!` | below 1: 7 (LibreOffice) or `#NUM!` (Gnumeric) |
| LARGE-012 | `=LARGE(A1:B5,1+2^-52)` | 7 | k at 15 digits (7) or plain ceiling (6) |
| SMALL-004 | `=SMALL(A1:A9,1.5)` | `#VALUE!` | 2 down, 3 up or nearest |
| SMALL-005 | `=SMALL(A1:A9,1.2)` | `#VALUE!` | 2 down or nearest, 3 up |
| SMALL-006 | `=SMALL(A1:A9,1+2^-52)` | 2 | k at 15 digits |
| MOD-010 | `=MOD(2^40,3)` | 1 | a quotient of 2^40 answered |
| MOD-011 | `=MOD(1125899999999,1)` | 0 | just below the reported limit |
| MOD-012 | `=MOD(1125900000000,1)` | `#NUM!` | at it |
| MOD-013 | `=MOD(3377699999997,3)` | 0 | the limit is on the quotient |
| MOD-014 | `=MOD(3377700000000,3)` | `#NUM!` | the same, at it |
| MOD-015 | `=MOD(2^53,3)` | `#NUM!` | far above |
| MOD-016 | `=MOD(1E+16,7)` | `#NUM!` | far above |
| MOD-017 | `=MOD(123456789012,0.7)` | 0.5000152587890625 | the formula in doubles, or an exact remainder (0.5000078322611261) |

## 8. Recorded, not changed

- **AGGREGATE's function-number list** (b4): Excel's lists 13 `MODE.SNGL` and 16 to 19 as well. They stay
  out (the engine answers `#VALUE!` for them by ADR-0047's note of 2026-10-10, and completion offers only
  numbers it evaluates, as `Subtotal_and_aggregate_list_only_what_the_engine_evaluates` pins). AGGREGATE-027
  to 031 now carry Excel's answers as `excelExpect` (`#N/A`, 34, 32, 34, 31.5), `observed`.
  *(Added after review, 2026-10-12: ADR-0047's note of 2026-10-10 says only that those numbers are
  `#VALUE!`, and no ADR records that completion leaves them out. So that a later reader sees the
  difference was decided, not overlooked, ADR-0058 gains a sentence after the bullet "`=SUM(A` lists the
  functions ExSheet computes … A deliberate difference.":)*

  > - **An argument's list offers only the values ExSheet computes** *(2026-10-12, the seventeenth Windows
  >   run, Part B's b4)*. Excel's list for `AGGREGATE`'s `function_num` offers 13 (`MODE.SNGL`) and 16 to
  >   19 (`PERCENTILE.INC`, `QUARTILE.INC`, `PERCENTILE.EXC`, `QUARTILE.EXC`). ExSheet's leaves them out,
  >   because it answers them `#VALUE!` (ADR-0047's note of 2026-10-10): offering them would make the
  >   Formula an error the moment one was chosen, as above. A deliberate difference.
- **The `SMALL` k the triage asked to round up** is refused instead: the sources disagree for `SMALL`
  (section 6), and no run has read it. Rounding up would be a guess between them.

## 9. Later questions (not asked; the entry refusal is not extended to them)

Reference-only positions of other functions, as corpus cases a later run could ask:
`=SUMIF(5,">1")`, `=SUMIFS(A1:A3,SEQUENCE(3),">1")`, `=COUNTIF(A1:A3*1,">1")`, `=COUNTIFS(5,1)`,
`=AVERAGEIF(SEQUENCE(3),">1")`, `=COUNTBLANK(5)`, `=OFFSET(5,0,0)`, `=ROW(5)`, `=COLUMN(SEQUENCE(2))`,
`=RANK.EQ(1,A1:A3*1)`. Each is `#VALUE!` (or the function's own answer) today.

## 10. Files outside the list

None. In `Functions.cs`, beyond `SUBTOTAL`, `AGGREGATE` and `RANK.EQ`, the declarations of `IF`, `INDEX`,
`IFS`, `SWITCH`, `CHOOSE`, `OFFSET`, `INDIRECT`, `LET` and `XLOOKUP` gained `ReturnsReference = true`: a
textual collision with another area that edits one of those lines is resolved by keeping both.
