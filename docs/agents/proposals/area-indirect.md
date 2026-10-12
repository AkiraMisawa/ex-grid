# The area pass, INDIRECT: what the orchestrator writes

The area pass of 2026-10-12 (`verification/2026-10-12-windows-area/report.md`, "Recorded (9)";
`tests/ExSheet.Engine.Tests/ExcelOracle/results-2026-10-12-area-com-en-US.json`) read Excel's answer to the four
`INDIRECT` cases the engine refused by decision. This file holds the text this area would put into ADR-0124, the
Definition of Done, the function catalogue, the engine's README, the Docs Site and `linked-tables.json`, none of which a
background agent changes. Each edit is exact and in whole lines, with the target's own indentation: the old lines, then
the new, each in a fence. Every other change is on the branch `claude/area-indirect`.

## What was done, per case

| Case | Formula | Excel | Before | Now | Why |
|---|---|---|---|---|---|
| INDIRECT-036 | in C2, `=INDIRECT("R[1]C[-1]",FALSE)` | 45 | `#VALUE!` (by decision) | 45, `observed` | ADR-0124's note: refused until a run observes the form; observed, and the answer is R1C1 notation's ordinary meaning, an offset from the Formula's own cell |
| INDIRECT-037 | `=SUM(INDIRECT("R3",FALSE))` | 45 | `#VALUE!` (by decision) | 45, `observed` | as 036: a row part alone is the whole row |
| INDIRECT-038 | `=SUM(INDIRECT("C2",FALSE))` | 118.333 | `#VALUE!` (by decision) | 118.333 (`digits` 9), `observed` | as 036: a column part alone is the whole column |
| INDIRECT-043 | `=SUM(INDIRECT("Positions"))` | 250.5 | `#VALUE!` (by decision) | `#VALUE!`, `excelExpect` 250.5, `observed` | kept refused: the answer settles one point only (below); reshaped as SORT-011 was after run 17 |

The rule INDIRECT-036 to 038 settle is implemented once, in `IndirectRowColumn` (`Functions.Lookup.cs`), generalised
as the seventeenth run's R3C2 was to ranges, case and qualifiers. The observed forms are an offset in brackets on both
axes (`R[1]C[-1]`), a row part alone (`R3`) and a column part alone (`C2`). What follows from them, and is read:

- R and C each take a number (absolute, from 1) or an offset in brackets other than 0, `[n]` or `[-n]` (from the
  Formula's own cell, which `Evaluator.Self` gives, read only).
- A row part alone is a whole row, and a column part alone a whole column.
- Two parts of the same kind joined by `:` name the rectangle they span.
- A number past the Sheet's edge names no place, `#REF!`, as `XFE1` does in A1 style (INDIRECT-017, observed), and so
  does another Sheet's name, whatever an offset beside them would read: both readings of an offset give `#REF!` there.

Still `#VALUE!`, because the observed forms leave Excel's answer open (a case below asks each):

- An R or a C with no number (`RC[-1]`, `R[2]C`, `R`): no observed form has one, and it could mean an offset of 0, the
  Formula's own row or column, or every row or column, as a part with no R or no C does.
- A zero offset in brackets (`R[0]`, `R[-0]`, `C[0]`, `C[-0]`): no observed form has one, and forms written like it,
  `R[+1]` and `R[01]`, are refused.
- A number written with a leading 0 or 0 itself (`R03C2`, `R[01]C`, `R0C2`), and an offset written with `+` (`R[+1]C`).
- An offset past the Sheet's edge (`R[-5]C[-1]` from C2): Excel might name no place or wrap it round to the Sheet's far
  side. A number past the edge, or another Sheet's name, beside it is `#REF!`, which both readings give.
- A cell joined to a whole row or column (`R2C2:R3`), or a row joined to a column (`R3:C2`).
- Structured or `#` text in R1C1 style.

Text that is not R1C1 notation is `#REF!`, as before.

`INDIRECT` is volatile, so a Formula an insertion moves counts its offsets from its new cell at once
(`IndirectTests.ADR0124_relative_r1c1_text_counts_from_the_formulas_cell_after_it_moves`). A Formula evaluated in no
cell has no own cell, and relative text there is `#VALUE!`, as `ROW()` is; every Formula of a Sheet is evaluated in its
cell, so no Consumer meets it.

**One reading to confirm.** The triage's "two parts joined by `:` name the rectangle they span" is implemented for two
parts of the same kind (cell and cell, row and row, column and column), which are the forms its cases name. A cell
joined to a whole row (`R2C2:R3`) has no A1 counterpart a Formula takes (`B2:3` is not a Reference), and Excel could
read it as the range operator's rectangle (`A2:XFD3`) or as no place; it stays `#VALUE!`, and INDIRECT-058 asks it. A
row joined to a column (`R3:C2`) is refused for the same reason, and INDIRECT-062 asks it.

### New cases (all `uncertain`)

The forms that follow from the observed ones, with the engine's answer, over Microsoft's A2:B5 fixture. None reads
the Formula's own cell, row or column, which is a cycle (`#CIRC!` differs from Excel by decision).

| Case | Formula | Engine (expect) | What Excel's answer would say |
|---|---|---|---|
| INDIRECT-045 | in C3, `=INDIRECT("R[-1]C[-1]",FALSE)` | 1.333 | a negative offset on both axes |
| INDIRECT-046 | in C2, `=SUM(INDIRECT("R[1]",FALSE))` | 45 | a whole row by offset |
| INDIRECT-047 | in C2, `=SUM(INDIRECT("C[-1]",FALSE))` | 118.333 | a whole column by offset |
| INDIRECT-048 | `=SUM(INDIRECT("R2:R3",FALSE))` | 46.333 | rows joined |
| INDIRECT-049 | `=SUM(INDIRECT("C1:C2",FALSE))` | 123.333 | columns joined (A5 holds the number 5) |
| INDIRECT-050 | in C2, `=INDIRECT("R3C[-1]",FALSE)` | 45 | an absolute row with a relative column |

INDIRECT-044, 051 and 052 were written with them, as forms the engine read, and are now among the refused forms
below: 044 and 051 have an R with no number, and 052 is an offset past the edge, rewritten from `R[-5]C` to
`R[-5]C[-1]` so that it asks that alone (the case had not yet been put to Excel).

What a declared Linked Table's bare name reads (INDIRECT-043's open question), over table Positions with rows
`["R-1",250.5]` and `["R-2",50]`, each with `engineDiffersByDecision` so that a pass records Excel's answer:

| Case | Formula | Engine (expect) | If it is the table's data (every column, no header) |
|---|---|---|---|
| INDIRECT-053 | `=COLUMNS(INDIRECT("Positions"))` | `#VALUE!` | 2 (1 if one column) |
| INDIRECT-054 | `=ROWS(INDIRECT("Positions"))` | `#VALUE!` | 2 (3 if the header is in it) |
| INDIRECT-055 | `=INDEX(INDIRECT("Positions"),1,1)` | `#VALUE!` | `R-1` (`Id` if the header is in it) |
| INDIRECT-056 | `=COUNTA(INDIRECT("Positions"))` | 1 | 4 |
| INDIRECT-057 | `=INDIRECT("Positions")` in ZZ1000, checked at AAA1000 | blank | 250.5 |

INDIRECT-056's expect is 1, not `#VALUE!`: the engine's `COUNTA` counts the refusal as one Value, as it counts any
Error Value (and as Excel's counts `INDIRECT("no place")`'s `#REF!`). INDIRECT-057's is blank: the refusal does not
spill. Both are the engine's present answers, as the corpus requires.

The R1C1 forms refused, with `engineDiffersByDecision`, so that a pass records Excel's answer and the next note can
settle them:

| Case | Formula | Engine (expect) | Excel's answer if the form is read as its neighbours are |
|---|---|---|---|
| INDIRECT-044 | in C2, `=INDIRECT("RC[-1]",FALSE)` | `#VALUE!` | 1.333, B2, if an R with no number is the own row; `#SPILL!` if it is every row |
| INDIRECT-051 | in C2, `=SUM(INDIRECT("RC[-1]:R[1]C[-1]",FALSE))` | `#VALUE!` | 46.333, B2:B3, if an R with no number is the own row |
| INDIRECT-052 | in C2, `=INDIRECT("R[-5]C[-1]",FALSE)` | `#VALUE!` | `#REF!` if it names no place; 0 if Excel wraps it round (B1048573 is blank) |
| INDIRECT-058 | `=SUM(INDIRECT("R2C2:R3",FALSE))` | `#VALUE!` | 46.333, the rectangle spanned |
| INDIRECT-059 | `=INDIRECT("R03C2",FALSE)` | `#VALUE!` | 45, as R3C2 |
| INDIRECT-060 | `=SUM(INDIRECT("R1C1#",FALSE))` over A1 `=SEQUENCE(3)` | `#VALUE!` | 6, as A1# |
| INDIRECT-061 | `=INDIRECT("Positions[PV]",FALSE)` | `#VALUE!` | 250.5, as in A1 style |
| INDIRECT-062 | `=ROWS(INDIRECT("R3:C2",FALSE))` | `#VALUE!` | 1048576, the whole Sheet the range operator gives (`#REF!` if it names no place) |
| INDIRECT-063 | in C2, `=INDIRECT("R[+1]C[-1]",FALSE)` | `#VALUE!` | 45, as R[1]C[-1] |
| INDIRECT-064 | in C2, `=INDIRECT("R[0]C[-1]",FALSE)` | `#VALUE!` | 1.333, B2, the own row |
| INDIRECT-065 | in C2, `=INDIRECT("R[-0]C[-1]",FALSE)` | `#VALUE!` | 1.333, B2, the own row |
| INDIRECT-066 | in B1, `=INDIRECT("R[2]C",FALSE)` | `#VALUE!` | 45, B3, if a C with no number is the own column; `#SPILL!` if it is every column |

## INDIRECT-043: Excel's answer is the evidence, and the decision it needs

`=SUM(INDIRECT("Positions"))` is 250.5 in Excel over a table of one row, `Id` `R-1` and `PV` 250.5. That shows only
that the bare name reads something whose numbers sum to the PV column: over one row whose other column is text, `SUM`
cannot tell the table's data (every column, no header) from a single column. And the run-17 rule, "read as the typed
one is read", has nothing to follow: ExSheet has no typed bare table name. Typed, `=Positions` and
`=COLUMNS(Positions)` are `#NAME?` (checked on this branch), and ADR-0049 reads a Linked Table by column (rule 2, "A
whole column is reached by name"). So the refusal stays, and INDIRECT-053 to 057 ask what the name reads.

**For the user, not decided here:** what a declared Linked Table's bare name reads, typed and through `INDIRECT`.
Points the decision meets:

- Excel reads `Positions` as the table's data, a Reference to every column without the header. Admitting it would
  give ExSheet a whole-table read, which ADR-0049 does not have: `INDEX(Positions,2,1)` reads a row by position, where
  rule 2 says a row is reached by key. (`INDEX(Positions[PV],2)` already reads one column by position.)
- Typed and through `INDIRECT` the refusal differs today: `#NAME?` typed (as an unknown name, though the table is
  declared) and `#VALUE!` through text. Whatever is decided, the two should agree.
- A refusal written as an Error Value is counted by `COUNTA` (INDIRECT-056 is 1 where Excel is probably 4), and caught
  by `IFERROR`. That is so for every refusal of ADR-0047, not this one only; whether a refusal should pass through
  them as `#CIRC!` and `#GETTING_DATA` do is ADR-0047's question, raised here because this case shows it.

### `linked-tables.json`: a typed case to propose (the file is in no area)

Appended after TABLE-023, so that the next pass reads Excel's answer to the typed form beside INDIRECT-053:

```json
    {
      "id": "TABLE-024",
      "source": "uncertain",
      "description": "Refused until Excel is asked: a declared table's bare name typed, which Excel reads as the table's data (2 columns); ExSheet has no typed bare table name, so it is #NAME? as an unknown name is",
      "tables": {"Positions": {"columns": ["Id", "PV"], "rows": [["R-1", 250.5], ["R-2", 50]]}},
      "cells": {"A1": "=COLUMNS(Positions)"},
      "check": "A1",
      "expect": {"value2": "#NAME?"},
      "engineDiffersByDecision": "ADR-0049 (rule 2: a Linked Table is read by key, and a whole column by name): a table's bare name typed is not offered and is #NAME?; recorded, so that a run reads Excel's answer beside INDIRECT-053"
    }
```

The engine's answer, `#NAME?`, was checked on this branch (`=COLUMNS(Positions)`, `=Positions` and
`=SUM(Positions)` over a declared table with data are each `#NAME?`).

## ADR-0124: the note of 2026-10-12, one sentence

`docs/adr/0124-volatile-functions-are-recalculated-after-every-change.md`, the three lines that hold the sentence
"**R1C1 text in a form no run has observed stays `#VALUE!`**". Old:

```markdown
does not spill. **R1C1 text in a form no run has observed stays `#VALUE!`**: relative (`R[1]C[-1]`, `RC`), whole rows
or columns (`R3`, `C2`), a number with a leading 0, and structured or `#` text in R1C1 style. INDIRECT-030 to 042 put
the forms generalised from the observed ones to the next run. Excel reads R1C1 text in the letters of its display
```

New:

```markdown
does not spill. **R1C1 text in a form no run has observed stays `#VALUE!`**, unless it follows from observed ones by
the rule they settle, as a range of two cells follows from one: a number with a leading 0, structured or `#` text in
R1C1 style, and the forms the note below names. Relative text (`R[1]C[-1]`, `RC`) and whole rows or columns (`R3`,
`C2`) were refused with them until the area pass of the same day observed `R[1]C[-1]`, `R3` and `C2`; `RC`, an R and
a C with no number, was not among them and stays refused. INDIRECT-030 to 042 put the forms generalised from the
observed ones to the next run. Excel reads R1C1 text in the letters of its display
```

## ADR-0124: a dated note after it, at the end of "`RAND`, `RANDBETWEEN` and `INDIRECT`"

The section is the ADR's last. After its last line, `Formula twice, and one that moves no spill once.)*`, a blank line
and:

```markdown
*(2026-10-12, after the area pass (ADR-0208's remedy): **R1C1 text is read in the three forms Excel was observed to
read, and in those that follow from them by the rule they settle.** In C2, `INDIRECT("R[1]C[-1]",FALSE)` is 45, B3's
Value: an offset in brackets counts from the Formula's own cell. `SUM(INDIRECT("R3",FALSE))` is 45, row 3, and
`SUM(INDIRECT("C2",FALSE))` is 118.333, column B (INDIRECT-036 to 038). What follows, generalised as R3C2 was: R and C
each take a number or an offset in brackets (`[n]`, `[-n]`); a row part alone is a whole row, a column part alone a
whole column; two parts of the same kind joined by `:` name the rectangle they span. INDIRECT-045 to 050 put those
answers to the next run. A number past the Sheet's edge names no place, `#REF!`, as `XFE1` does (INDIRECT-017),
whatever an offset beside it would read. `INDIRECT` is volatile, so a Formula a structural edit moves counts from its
new cell. **Still `#VALUE!`, since the observed forms leave Excel's answer open**: an R or a C with no number
(`RC[-1]`, `R[2]C`), which could be the Formula's own row or column or every one; a zero offset in brackets (`R[0]`,
`R[-0]`); a number written with a leading 0 or an offset with a `+`; an offset past the Sheet's edge, which Excel
might call no place or wrap round to the Sheet's far side; a cell joined to a whole row or column (`R2C2:R3`) or a row
to a column (`R3:C2`); and structured or `#` text in R1C1 style. INDIRECT-044, 051, 052 and 058 to 066 ask them. **A
declared Linked Table's bare name stays `#VALUE!`.** Excel's `SUM(INDIRECT("Positions"))` is 250.5 (INDIRECT-043), but
over a table of one row whose other column is text that does not tell the table's data from one column, and ExSheet
has no typed bare table name to read it as: typed, `Positions` is `#NAME?`, and
[ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md) reads a Linked Table by column. INDIRECT-053
to 057 ask what it reads; following it then wants a decision on what a bare name reads, typed and through
`INDIRECT`.)*
```

## The engine's README (`src/ExSheet.Engine/README.md`)

The function table's row. Old:

```markdown
| `INDIRECT` | `ref_text, [a1]` — text naming a place on the same Sheet, A1 or absolute R1C1, a Linked Table's column or `A1#`; volatile (ADR-0124) |
```

New:

```markdown
| `INDIRECT` | `ref_text, [a1]` — text naming a place on the same Sheet, A1 or R1C1 (offsets in brackets, whole rows and columns included), a Linked Table's column or `A1#`; volatile (ADR-0124) |
```

The paragraph under "`INDIRECT` reads A1-style text naming a cell or a range of this Sheet". Old:

```markdown
  never it, so `INDIRECT("B5")` names B5 whatever moved. It also reads the forms the seventeenth
  Windows run observed: R1C1 text (`a1` FALSE, 0, a blank or left empty) naming an absolute cell or a
  range of two, a structured reference's text as `Table[Column]` typed is read, waiting while the
  table waits, and `A1#` as typed `A1#` is read. R1C1 text that is relative or names whole rows or
  columns, and a declared Linked Table's bare name (`Positions`, which Excel reads as the table's
  data), are `#VALUE!` until a run observes them, and text that names no place is `#REF!`, as in Excel:
  another Sheet's name, a table never declared or a column it lacks. What a computed Reference
```

New:

```markdown
  never it, so `INDIRECT("B5")` names B5 whatever moved. It also reads the forms the seventeenth
  Windows run and the area pass observed, and those that follow from them by the rule they settle:
  R1C1 text (`a1` FALSE, 0, a blank or left empty) with R and C each a number or an offset in
  brackets from the Formula's own cell, a row part alone a whole row, a column part alone a whole
  column, and two parts of the same kind joined by `:` the rectangle they span; a structured
  reference's text as `Table[Column]` typed is read, waiting while the table waits; and `A1#` as
  typed `A1#` is read. `#VALUE!` until a run observes them: R1C1 text with an R or a C and no number
  (`RC[-1]`), a zero offset in brackets (`R[0]`), a number written with a leading 0 or an offset with
  a `+`, an offset past the Sheet's edge (which Excel might call no place or wrap round), a cell
  joined to a whole row or column or a row to a column, or structured or `#` text; and a declared
  Linked Table's bare name (`Positions`, which Excel reads as the table's data). Text that names no
  place is `#REF!`, as in Excel: another Sheet's name, a table never declared, a column it lacks, or
  a row or column number past the Sheet's edge, as `XFE1` is. What a computed Reference
```

## The function catalogue (`docs/specs/exsheet-functions/spec.md`, the `INDIRECT` row)

Old:

```markdown
| `INDIRECT` | Supported | — | Volatile, as `OFFSET`; A1-style text naming a place on the same Sheet, not rewritten by an insertion; absolute R1C1 text, a Linked Table's column as structured text and `A1#`, as the seventeenth run observed; relative and whole-row or whole-column R1C1 text is `#VALUE!`, and text that names no place is `#REF!` (ADR-0124, 2026-10-10 and 2026-10-12). Ticket 18 |
```

New:

```markdown
| `INDIRECT` | Supported | — | Volatile, as `OFFSET`; A1-style text naming a place on the same Sheet, not rewritten by an insertion; R1C1 text (each R and C a number or an offset in brackets from the Formula's own cell; a cell, a whole row or column, or the rectangle two parts of the same kind span), a Linked Table's column as structured text and `A1#`, as the seventeenth run and the area pass observed and by the rule they settle; `#VALUE!` for a Linked Table's bare name and for R1C1 text with an R or a C and no number, a zero offset in brackets, a number written with a leading 0 or an offset with a `+`, an offset past the Sheet's edge, a cell joined to a whole row or column or a row to a column, or structured or `#` text; `#REF!` for text that names no place, a row or column number past the Sheet's edge included (ADR-0124, 2026-10-10 and 2026-10-12). Ticket 18 |
```

## The Definition of Done, SH-68: the `INDIRECT` clause

The whole SH-68 line of `docs/definition-of-done.md`; only its `INDIRECT` sentence changes. Old:

```markdown
| **SH-68** | MUST | `SUBTOTAL` and `AGGREGATE` answer their corpus cases while no row can be hidden, and a test named for ADR-0047 fails on the day one can. Inside their range, a cell whose Formula, or whose Spill Range's Anchor's, calls `SUBTOTAL` or `AGGREGATE` anywhere is nested: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it and options 4 to 7 count its Value, as the seventeenth Windows run read Excel's answers; an `AGGREGATE` function number for a function the engine does not support is `#VALUE!`, and a Formula that gives one of their reference-only arguments something that can never be a Reference is refused on entry. `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and Excel's numbers for its newer Error Values: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14. `INDIRECT` reads A1-style text naming a place on the same Sheet, R1C1 text naming an absolute cell or a range of two, a Linked Table's column as structured text (waiting while the table does) and `A1#`, keeps its text through an insertion or deletion, gives `#VALUE!` for R1C1 text that is relative or names whole rows or columns, and `#REF!` for text that names no place, a table or a column that does not exist included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12) *(2026-10-11, decided with the user: for `AGGREGATE`'s options 4 to 7, a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's Windows run reads Excel's answer, which Microsoft documents as counted; then it follows Excel.)* *(2026-10-12: the seventeenth run read it — 6, counted — and the engine follows Excel, ADR-0047's note of that date.)* *(2026-10-12, the seventeenth Windows run read `ERROR.TYPE`'s numbers for Excel's newer Error Values, which the row had refused with `#VALUE!` until Excel was asked; it now follows Excel, ADR-0047's note of this date.)* | Layer 1 | as stated |
```

New:

```markdown
| **SH-68** | MUST | `SUBTOTAL` and `AGGREGATE` answer their corpus cases while no row can be hidden, and a test named for ADR-0047 fails on the day one can. Inside their range, a cell whose Formula, or whose Spill Range's Anchor's, calls `SUBTOTAL` or `AGGREGATE` anywhere is nested: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it and options 4 to 7 count its Value, as the seventeenth Windows run read Excel's answers; an `AGGREGATE` function number for a function the engine does not support is `#VALUE!`, and a Formula that gives one of their reference-only arguments something that can never be a Reference is refused on entry. `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and Excel's numbers for its newer Error Values: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14. `INDIRECT` reads A1-style text naming a place on the same Sheet, R1C1 text naming a cell, a whole row or column, or the rectangle two parts of the same kind span, each R and C a number or an offset in brackets from the Formula's own cell, a Linked Table's column as structured text (waiting while the table does) and `A1#`, keeps its text through an insertion or deletion, gives `#VALUE!` for a Linked Table's bare name and for R1C1 text with an R or a C and no number, a zero offset in brackets, a number written with a leading 0, an offset written with a `+`, an offset past the Sheet's edge, a cell joined to a whole row or column, a row joined to a column, or structured or `#` text, and `#REF!` for text that names no place, a table or a column that does not exist and a row or column number past the Sheet's edge included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12) *(2026-10-11, decided with the user: for `AGGREGATE`'s options 4 to 7, a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's Windows run reads Excel's answer, which Microsoft documents as counted; then it follows Excel.)* *(2026-10-12: the seventeenth run read it — 6, counted — and the engine follows Excel, ADR-0047's note of that date.)* *(2026-10-12, the seventeenth Windows run read `ERROR.TYPE`'s numbers for Excel's newer Error Values, which the row had refused with `#VALUE!` until Excel was asked; it now follows Excel, ADR-0047's note of this date.)* | Layer 1 | as stated |
```

The `INDIRECT` sentence alone, old:

> `INDIRECT` reads A1-style text naming a place on the same Sheet, R1C1 text naming an absolute cell or a range of two, a Linked Table's column as structured text (waiting while the table does) and `A1#`, keeps its text through an insertion or deletion, gives `#VALUE!` for R1C1 text that is relative or names whole rows or columns, and `#REF!` for text that names no place, a table or a column that does not exist included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12)

and new:

> `INDIRECT` reads A1-style text naming a place on the same Sheet, R1C1 text naming a cell, a whole row or column, or the rectangle two parts of the same kind span, each R and C a number or an offset in brackets from the Formula's own cell, a Linked Table's column as structured text (waiting while the table does) and `A1#`, keeps its text through an insertion or deletion, gives `#VALUE!` for a Linked Table's bare name and for R1C1 text with an R or a C and no number, a zero offset in brackets, a number written with a leading 0, an offset written with a `+`, an offset past the Sheet's edge, a cell joined to a whole row or column, a row joined to a column, or structured or `#` text, and `#REF!` for text that names no place, a table or a column that does not exist and a row or column number past the Sheet's edge included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12)

## The Docs Site (`samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`, the `INDIRECT` bullet)

Old:

```html
                rewrites the References beside it, never the text. It also reads what Excel was seen to read: R1C1
                text (<code>a1</code> FALSE) naming a cell or a range, <code>R3C2</code> or <code>R2C2:R5C2</code>; a
                Linked Table's column as text, <code>Positions[PV]</code>, which waits while the table does; and a
                Spill Range, <code>A1#</code>. R1C1 text that is relative, <code>R[1]C[-1]</code>, or names whole rows
                or columns, and a Linked Table's name on its own (<code>"Positions"</code>, which Excel reads as the
                table's data), are <code>#VALUE!</code> until Excel has been asked; text that names no place, another
                Sheet's name or a table that does not exist included, is <code>#REF!</code>, as in Excel.
```

New:

```html
                rewrites the References beside it, never the text. It also reads what Excel was seen to read, and what
                follows from it: R1C1 text (<code>a1</code> FALSE) naming a cell or a range, <code>R3C2</code> or
                <code>R2C2:R5C2</code>, counted from the Formula's own cell by offsets in brackets,
                <code>R[1]C[-1]</code>, or naming whole rows and columns, <code>R3</code>, <code>C[-1]</code> or
                <code>R2:R3</code>; a Linked Table's column as text, <code>Positions[PV]</code>, which waits while the
                table does; and a Spill Range, <code>A1#</code>. R1C1 text in a form those leave open is
                <code>#VALUE!</code> until Excel has been asked: an R or a C with no number, <code>RC[-1]</code>; a
                zero offset, <code>R[0]C[-1]</code>; a number written with a leading 0 or an offset with a
                <code>+</code>, <code>R03C2</code> or <code>R[+1]C[-1]</code>; an offset past the Sheet's edge,
                <code>R[-5]C[-1]</code> from row 2, which Excel might call no place or wrap round to the last rows; a
                cell joined to a whole row or column, <code>R2C2:R3</code>, or a row to a column, <code>R3:C2</code>;
                and a Linked Table's column or a Spill Range written in R1C1 style, <code>Positions[PV]</code> or
                <code>R1C1#</code> with <code>a1</code> FALSE. So is a Linked Table's name on its own
                (<code>"Positions"</code>, which Excel reads as the table's data), until Excel has been asked what it
                reads. Text that names no place, another Sheet's name, a row or column number past the Sheet's edge
                or a table that does not exist included, is <code>#REF!</code>, as in Excel.
```

No Example shows R1C1 text, so none changes.

## Ticket 18 and the Change Note (written on the branch)

- `docs/specs/exsheet-functions/issues/18-indirect.md`: the open box for INDIRECT-030 to 043 is ticked with the area
  pass's answer, a box for the R1C1 rule is added and ticked (the observed forms, those that follow from them, and the
  forms they leave open, refused), and a new open box asks INDIRECT-044 to 066; a comment of 2026-10-12 records the
  pass, the bare-name evidence, and what review refused.
- `docs/releases/unreleased/area-indirect-r1c1-relative-rows-columns.md` (ExSheet, Behaviour,
  `exsheet/formulas#supported-functions`, ADR-0124).
