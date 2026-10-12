# Run 17, lookup and arrays: what the orchestrator writes

The seventeenth Windows run (`verification/2026-10-11-windows-17/report.md`, Part A) put eight cases of this area to
Excel. This file holds the text this area would put into the ADRs, the Definition of Done, the specification, the engine's
README and the Docs Site, which a background agent does not change. Each block says where it goes; the text is in the
repository's voice, ready to paste.

## What was done, per case

| Case | Formula | Excel | Before | Now | Why |
|---|---|---|---|---|---|
| INDIRECT-023 | `=INDIRECT("R3C2",FALSE)` | 45 | `#VALUE!` | 45, `observed` | ADR-0124's note: refused until a run observes the form; observed |
| INDIRECT-024 | `=INDIRECT("R3C2",0)` | 45 | `#VALUE!` (by decision) | 45, `observed` | as 023 |
| INDIRECT-025 | `=INDIRECT("Positions[PV]")` | 250.5 | `#VALUE!` (by decision) | 250.5, `observed` | as 023 |
| INDIRECT-026 | `=SUM(INDIRECT("A1#"))` | 6 | `#VALUE!` (by decision) | 6, `observed` | as 023 |
| INDEX-012 | `=INDEX(G1:H5,2)` | `#REF!` | `Banana` (the row spilled) | `#REF!`, `observed` | the case was the engine's guess; ADR-0047 |
| XMATCH-008 | `=XMATCH("Banana",G1:G5,0,2)` | 2 | `#VALUE!` | `#VALUE!`, `excelExpect` 2, `observed` | ADR-0047, "Settled while building": XLOOKUP's binary search over unsorted data is refused; Excel's algorithm is undocumented (XLOOKUP-116 to 122 carry Excel's answer the same way; 048 to 057 are recorded without it) |
| SORT-011 | `=SORT(Q1:Q2)`, Q1 7, Q2 blank | 7 | `#VALUE!` | `#VALUE!`, `excelExpect` 7, `observed` | ADR-0047's refusal (its paragraph admitting `SORT` and `UNIQUE`), kept as ADR-0202 allows: the run read only the Anchor (below). The corpus cites "ADR-0202, ADR-0047" (it cited ADR-0125, which holds no such statement, before review) |
| UNIQUE-009 | `=UNIQUE(Q1:Q2)` | 7 | `#VALUE!` | `#VALUE!`, `excelExpect` 7, `observed` | as SORT-011 |

Two gaps the observed forms made visible were closed with them (outside this area's files, each a few lines):

- `Sheet.cs`, the recalculation's reader: a Linked Table met through `INDIRECT`'s text while it waits marks the Formula
  as waiting, as a waiting cell met through a computed Reference already does, so `IFERROR(INDIRECT("Rates[Mid]"),0)`
  waits instead of showing 0.
- `Sheet.Spill.cs`: after each layout of the spills that changes something, the volatile Formulas are computed once
  more, with the layout's Dependents. What `INDIRECT` or `OFFSET` computes a Reference to is no Dependent of the cells
  the layout changed, so before this a Formula reading a spilled cell that way kept its old Value: with A1
  `=SEQUENCE(3)`, `=SUM(OFFSET(B1,0,-1,3,1))` stayed 3 after A3 blocked the spill, where `=SUM(A1:A3)` showed `#SPILL!`.
  This was there before the run (`INDIRECT("A3")` and `OFFSET`), and `A1#` text would have shown it at once. Each
  computation draws and reads the clock as the first did (ADR-0180's draw depends on the change count, not the pass).
- *Corrected after review (2026-10-12).* The first version of that change made a blocked Anchor whose own Formula is
  volatile (`=INDIRECT("C1:C3")`, `=OFFSET(C1,0,0,3,1)`, `=SEQUENCE(3)+0*RAND()`) show its array's first Value instead
  of `#SPILL!`: each volatile pass staged the first Value, each layout put `#SPILL!` back and reported the Anchor
  changed, and the loop stopped at its 64th pass, after a pass and before a layout. Every volatile Formula was computed
  64 times while such a spill stood. Now an Anchor computed again with an array of the shape its last layout blocked
  stays `#SPILL!` (`Sheet.cs`, the pass), so computing it again changes nothing, and the recalculation always stops on
  a layout. **The cost, stated:** every volatile Formula is computed once, and once more after each layout that
  changes something — twice when a change blocks or frees one spill, once when no spill moves, one more for each
  further spill that a layout moves (`SpillTests.ADR0124_volatile_formulas_are_computed_once_more_per_layout_that_changes`
  pins 1, 2 and 3). The engine's cap of 64 passes is unchanged. What the extra pass costs on a large Sheet belongs to
  SH-19's measurement.
- *Found in review, not changed here.* A Formula that reads its own Spill Range through a typed Reference,
  `A1` `=IF(A2=0,SEQUENCE(2),5)`, never settles: each layout changes what the next pass computes, and the
  recalculation stops at the cap with `5` in A1 and A2 blank. ADR-0125's "Dependencies" makes a reader of a spilled
  cell depend on the Anchor, which would make this a cycle (`#CIRC!`, ADR-0047; Excel reports a circular reference),
  but the dependency graph does not route a spilled cell to its Anchor. It was there before this run, and it is no
  run-17 case; it wants a ticket of its own.
- `INDEX`: the rule INDEX-012 observed tests for a Reference, and a Spill Range is one: `=INDEX(A1#,2)` and
  `=INDEX(INDIRECT("A1#"),2)` over `=SEQUENCE(2,2)` are `#REF!` as `=INDEX(A1:B2,2)` is (the first version spilled
  `{3,4}` for both). `A1#` evaluates to the array the Anchor computed, so the operand now says it is a Spill Range.
  INDEX-017 and INDEX-018 ask Excel, `uncertain`.

## ADR-0124: a dated note at the end of "`RAND`, `RANDBETWEEN` and `INDIRECT`"

> *(2026-10-12, after the seventeenth Windows run: **the three refused forms were observed, and `INDIRECT` reads them as
> Excel does.** `INDIRECT("R3C2",FALSE)` and `INDIRECT("R3C2",0)` are 45, `INDIRECT("Positions[PV]")` is 250.5 and
> `SUM(INDIRECT("A1#"))` over `=SEQUENCE(3)` is 6 (INDIRECT-023 to 026). R1C1 text — `a1` FALSE, 0, a blank or left
> empty — naming an absolute cell, or a range of two such cells, is the Reference it names, with or without the Sheet's
> own qualifier. A structured reference's text is the Linked Table's column, read as `Table[Column]` typed is: a table
> still waiting for its data waits, even under `IFERROR`, and a table never declared, or a column it does not have,
> names no place, `#REF!`. `A1#` is the Spill Range of the Formula in A1, read as `A1#` typed is, and `#REF!` where it
> does not spill. **R1C1 text in a form no run has observed stays `#VALUE!`**: relative (`R[1]C[-1]`, `RC`), whole rows
> or columns (`R3`, `C2`), a number with a leading 0, and structured or `#` text in R1C1 style. INDIRECT-030 to 042 put
> the forms generalised from the observed ones to the next run. Excel reads R1C1 text in the letters of its display
> language (`Z1S1` in a German Excel); ExSheet reads R and C in every culture, as its function names are English in
> every culture, and the run's Excel was English. **A volatile Formula is computed once more after each layout of the
> spills that changes something**, since what it computes a Reference to is no Dependent of the cells the layout
> changed: a Reference `INDIRECT` or `OFFSET` computes into a Spill Range follows it when it is blocked or freed, as a
> typed one does. An Anchor computed again with an array of the shape its layout blocked stays `#SPILL!`, so the
> recalculation ends on a layout that changes nothing: a change that blocks or frees one spill computes each volatile
> Formula twice, and one that moves no spill once.)*

## ADR-0125: a dated note at the end of "What becomes Excel's answer"

> *(2026-10-12, after the seventeenth Windows run: **`INDEX` over a Reference of several rows and several columns, with
> `column_num` left out, is `#REF!`**, as Excel's `=INDEX(G1:H5,2)` is (INDEX-012); a row or a column of 0 still spills
> (INDEX-013), and over an array `row_num` alone is still the whole row, as Microsoft documents (INDEX-014). A Spill
> Range of several rows and columns, `A1#` typed or `INDIRECT("A1#")`, is a Reference as a typed range is, and takes the
> same `#REF!` (INDEX-017 and 018 ask it). Whether `column_num` left empty, `=INDEX(G1:H5,2,)`, is the whole row, and
> whether a row of 0 with it left out is `#REF!` too, are INDEX-015 and 016 for the next run. **A blank among `SORT`'s
> and `SORTBY`'s keys, and in `UNIQUE`'s array, stays refused with `#VALUE!`**, as ADR-0047's paragraph admitting them
> under this ADR wrote, kept before the tag under ADR-0202. Excel answers 7 for `=SORT(Q1:Q2)` and `=UNIQUE(Q1:Q2)` over 7 and a blank
> (SORT-011, UNIQUE-009), and the Anchor shows only that a blank does not sort first as 0 would. Following it would
> guess where the blank goes, what its spilled cell shows, how a descending sort places it and whether `UNIQUE` counts
> it one of a kind with 0; SORT-012 to 014 and UNIQUE-010 and 011 ask those.)*

## ADR-0047: a line under "Settled while building" (optional)

> *(2026-10-12: `XMATCH`'s binary search refuses as `XLOOKUP`'s does. Over `G1:G5` unsorted, Excel's
> `=XMATCH("Banana",G1:G5,0,2)` is 2 (XMATCH-008), recorded as Excel's answer beside the refusal, as XLOOKUP-116 to
> 122 are.)*

## Definition of Done, SH-68: the `INDIRECT` clause

Replace

> `INDIRECT` reads A1-style text naming a place on the same Sheet, keeps its text through an insertion or deletion,
> gives `#VALUE!` for R1C1, structured and `A1#` text, and `#REF!` for text that names no place (ADR-0047's and
> ADR-0124's notes of 2026-10-10)

with

> `INDIRECT` reads A1-style text naming a place on the same Sheet, R1C1 text naming an absolute cell or a range of two,
> a Linked Table's column as structured text (waiting while the table does) and `A1#`, keeps its text through an
> insertion or deletion, gives `#VALUE!` for R1C1 text that is relative or names whole rows or columns, and `#REF!` for
> text that names no place, a table or a column that does not exist included (ADR-0047's and ADR-0124's notes of
> 2026-10-10, ADR-0124's of 2026-10-12)

SH-59 may add after "a Reference it computes is read as it stands after the recalculation": ", a spill's layout
included".

## `docs/specs/exsheet-functions/spec.md`, the catalogue rows

- `INDEX` (its text predates ADR-0125 and is already stale): "Spills a whole row or column for a row or a column of 0
  (ADR-0125); over a range of several rows and several columns, `column_num` left out is `#REF!`, as Excel's is
  (seventeenth run)".
- `XMATCH`: "The modes as `XLOOKUP`'s, and its binary search refused where `XLOOKUP`'s is (Excel's answer recorded,
  XMATCH-008)".
- `INDIRECT`: "Volatile, as `OFFSET`; A1-style text naming a place on the same Sheet, not rewritten by an insertion;
  absolute R1C1 text, a Linked Table's column as structured text and `A1#`, as the seventeenth run observed; relative
  and whole-row or whole-column R1C1 text is `#VALUE!`, and text that names no place is `#REF!` (ADR-0124, 2026-10-10
  and 2026-10-12). Ticket 18".
- `SORT`, `SORTBY`, `UNIQUE`: add "; Excel's Anchor recorded (SORT-011, UNIQUE-009), the refusal kept until a run reads
  the rest".

## `src/ExSheet.Engine/README.md`

- The function table's `INDIRECT` row: "`ref_text, [a1]` — text naming a place on the same Sheet, A1 or absolute R1C1,
  a Linked Table's column or `A1#`; volatile (ADR-0124)".
- The bullet "**`INDIRECT` reads A1-style text naming a cell or a range of this Sheet**": replace its sentence "Text in a
  form Excel reads but no Windows run has observed is `#VALUE!` — R1C1 style (`a1` FALSE), a structured reference
  (`Positions[PV]`) and a Spill Range (`A1#`) — and text that names no place is `#REF!`, as in Excel, another Sheet's
  name included." with "It also reads the forms the seventeenth Windows run observed: R1C1 text (`a1` FALSE, 0, a blank
  or left empty) naming an absolute cell or a range of two, a structured reference's text as `Table[Column]` typed is
  read, waiting while the table waits, and `A1#` as typed `A1#` is read. R1C1 text that is relative or names whole rows
  or columns is `#VALUE!` until a run observes it, and text that names no place is `#REF!`, as in Excel: another Sheet's
  name, a table never declared or a column it lacks." Add to its last sentence: "and a Reference it computes into a
  Spill Range follows the spill's layout."
- The bullet "**Asked of Excel before they are answered**": change "`UNIQUE` with a blank in its array, and `SORT` and
  `SORTBY` with a blank or an Error Value among their keys (`#VALUE!`)" to "`UNIQUE` with a blank in its array, and
  `SORT` and `SORTBY` with a blank or an Error Value among their keys (`#VALUE!`; the seventeenth run read only the
  Anchor of a blank's sort)".
- The `XLOOKUP` binary search bullet: "**`XLOOKUP`'s and `XMATCH`'s binary search**".

## Docs Site, `/exsheet/formulas`

- **Supported functions**, the `INDIRECT` item. Replace "R1C1 text (`a1` FALSE), a structured reference's text and
  `A1#` are `#VALUE!` until Excel has been asked how it reads them; text that names no place, another Sheet's name
  included, is `#REF!`, as in Excel." with:

  > It also reads what Excel was seen to read: R1C1 text (`a1` FALSE) naming a cell or a range, `R3C2` or
  > `R2C2:R5C2`; a Linked Table's column as text, `Positions[PV]`, which waits while the table does; and a Spill Range,
  > `A1#`. R1C1 text that is relative, `R[1]C[-1]`, or names whole rows or columns is `#VALUE!` until Excel has been
  > asked; text that names no place, another Sheet's name or a table that does not exist included, is `#REF!`, as in
  > Excel.

  and after "would: one that reaches its own cell or a circular reference is `#CIRC!`," nothing changes; the item may
  end with "One that reaches into a Spill Range follows the spill as it is blocked and freed."
- **Supported functions**, the `XLOOKUP` binary search item: "**`XLOOKUP`'s and `XMATCH`'s binary search**".
- **Supported functions**, a new item beside it:

  > **A blank among `SORT`'s or `SORTBY`'s keys, or in `UNIQUE`'s array,** is `#VALUE!`. Excel sorts it, but where it
  > goes and what its cell shows have not been read yet.

- **Arrays that spill**: after "an `INDEX` with a row or a column of 0", add ". `INDEX` over several rows and columns
  with `column_num` left out is `#REF!`, as in Excel; write `=INDEX(G1:H5,2,0)` for the row".
- `/exsheet/functions` needs nothing: it is generated from the engine's function list, whose `INDIRECT`, `INDEX`,
  `XMATCH`, `SORT` and `UNIQUE` entries did not change.

No Docs Site Example uses `INDIRECT`, `INDEX`, `XMATCH`, `SORT` or `UNIQUE` (`samples/` searched), so no Example is
now false.

## Questions for the next run (a `-Area` run of `indirect`, `index`, `sort`, `unique`)

Each is a corpus case already, `uncertain` (the engine's answer) or recorded (the engine's refusal, Excel's answer read
without comparing):

- INDIRECT-030 `=SUM(INDIRECT("R2C2:R5C2",FALSE))` 118.333; 031 `"r3c2"` 45; 032 `"Sheet1!R3C2"` 45; 033 `a1` left
  empty 45; 034 `a1` a blank cell 45; 035 `INDIRECT("B3",FALSE)` `#REF!`; 036 `R[1]C[-1]` from C2 (recorded; Excel's 45
  if relative text reads from the Formula's cell); 037 `R3` and 038 `C2` summed (recorded); 039 `Positions[[PV]]`
  250.5; 040 `Missing[PV]` `#REF!`; 041 `Positions[Delta]` `#REF!`; 042 `A1#` over a constant `#REF!`.
- INDEX-014 `=INDEX(SEQUENCE(2,2),2)` 3 (documented); 015 `=INDEX(G1:H5,2,)` `Banana`; 016 `=INDEX(G1:H5,0)` `#REF!`;
  017 `=INDEX(J1#,2)` and 018 `=INDEX(INDIRECT("J1#"),2)` over `=SEQUENCE(2,2)`, `#REF!`.
- SORT-012 the second spilled cell of `=SORT(Q1:Q2)` (0 or empty text); SORT-013 `=SORT(Q1:Q2,1,-1)` (7 if a blank
  sorts last whatever the order); SORT-014 `=SORT(A1:A3)` over `b`, blank, `a` (`a` if a blank sorts last, not as empty
  text). UNIQUE-010 the second spilled cell of `=UNIQUE(Q1:Q2)`; UNIQUE-011 `=ROWS(UNIQUE(A1:A2))` over 0 and a blank (1
  if a blank is one of a kind with 0).

With those answered, the `SORT`/`UNIQUE` refusal can follow Excel on a rule rather than a guess, and the generalised
`INDIRECT` forms become `observed` or are corrected.
