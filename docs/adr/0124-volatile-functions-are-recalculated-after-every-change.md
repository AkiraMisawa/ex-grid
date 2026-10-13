# Volatile functions are recalculated after every change

*(Decided with the user on 2026-10-03, from the function catalogue's P2 Decide list,
`docs/specs/exsheet-functions/spec.md`.)*

[ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)
recalculates a Formula when a cell its text names changes. The engine builds the dependency graph
from those names. Some functions answer from something their text does not name:

- **`OFFSET`** computes the Reference it reads while it is evaluated. `=OFFSET(A1,B1,0)` reads A6
  when B1 is 5, and A6 is named nowhere, so a change to A6 would leave the Formula showing the old
  Value. That is the quiet wrong answer this project refuses.
- **`NOW`** answers the time, which moves while no cell changes.
- **`INDIRECT`**, later, reads a Reference written as text.

Excel calls these functions *volatile*, and recalculates every Formula that calls one whenever
anything in the workbook is recalculated.

## The decision

- **A Formula that calls a volatile function is recalculated in every recalculation,** whatever
  the change, together with everything that reads it. The volatile functions are `OFFSET` and
  `NOW`, and `INDIRECT` when it is admitted. `RAND` and `RANDBETWEEN` would be volatile too, but
  their answers are not deterministic, and they stay in the catalogue for their own decision.
  `TODAY` is not volatile: the Sheet Day changes only when it is set (ADR-0121).
- **A Reference computed while a Formula is evaluated is read as it stands after this
  recalculation.** If it names a cell that the same recalculation has still to compute, that cell
  is computed first, on demand. A Formula that reaches itself this way is `#CIRC!`
  (ADR-0047's cycle rule). The order of the work never shows in a Value (principle 6).
- **There is still one completed recalculation per change.** No Value is shown from a
  recalculation that has not finished (ADR-0047).
- **`ExSheet.Engine` keeps the moment as data, as it keeps the Sheet Day.** The engine reads no
  clock. Whoever runs it gives it a source of the current moment: ExSheet's is the clock of
  ADR-0121 read in the Sheet's zone. The engine reads that source once at the start of each
  recalculation, so every `NOW` in one recalculation answers the same moment.
- **ExSheet also moves `NOW` on once a minute,** at the minute's turn in the Sheet's zone, by a
  recalculation of the volatile Formulas alone. A Sheet with no volatile Formula is not
  recalculated by the clock. Excel moves `NOW` only when something is recalculated; this is a
  difference by decision, so that a time shown on a page left open is not hours old.
- **`NOW` without a zone is `#GETTING_DATA`,** as `TODAY` is (ADR-0121). A fixed `Today` does not
  fix `NOW`; a Consumer that wants a frozen moment gives ExSheet a `TimeProvider` that is frozen.

## `VALUE`, decided at the same time

`VALUE` reads its text under the Sheet's culture: the reading the arithmetic operators already
give text (`="1,5"+1` under de-DE is 2.5). Excel reads it in the system's locale, and ExSheet's
locale is the Sheet's ([ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)).

## Considered options

- **Leave `OFFSET` out, and move `NOW` by a timer alone.** Rejected with the user: `OFFSET` is the
  usual way to a range whose size follows the data, and a timer cannot make it right.
- **Track the References a volatile function computed, and recalculate it only when they change.**
  Rejected for now: the Reference can change with any of the function's arguments, so the
  tracking would have to be exact to be safe, and Excel's own answer is to recalculate.
- **`NOW` only on change, as Excel does.** Rejected with the user, as above.

## Consequences

- A Sheet with many volatile Formulas recalculates them on every change. How much that costs at
  a large Sheet is measured, not asserted (SH-19).
- `OFFSET`'s result can be a range of several cells. Until spilled arrays arrive
  ([ADR-0125](./0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md)), such a result
  is a range a function such as `SUM` reads, and `#VALUE!` where one Value is wanted.

## `RAND`, `RANDBETWEEN` and `INDIRECT` *(decided with the user, 2026-10-10)*

- **`RAND` and `RANDBETWEEN` are volatile,** and their draws change only at a change, never on the
  clock: [ADR-0180](./0180-rand-and-randbetween-draw-from-the-sheet-seed-and-draw-again-at-each-change.md).
- **`INDIRECT` is admitted for one form of text: an A1-style Reference to a cell or a range of the
  same Sheet,** with or without the Sheet's own qualifier. It is volatile, as decided above, and the
  place its text names is read as it stands after the recalculation.
  - **Its text is not rewritten by an insertion or a deletion.** `INDIRECT("A5")` names A5 after a
    row is inserted above row 5, while `=A5` becomes `=A6`: a Reference moves with its cell, and
    text is only text. Excel is used for exactly this, though no Windows run has recorded it yet; the
    next one does.
  - **Text in a form Excel reads but no Windows run has observed is `#VALUE!`:**
    - R1C1 style (`a1` given as FALSE);
    - a structured reference such as `Positions[PV]`;
    - a Spill Range's `A1#`.
  - **Text that names no place is `#REF!`, as in Excel.** That includes another Sheet's name, since
    an ExSheet holds one Sheet
    ([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)).

*(2026-10-12, after the seventeenth Windows run: **the three refused forms were observed, and `INDIRECT` reads them as
Excel does.** `INDIRECT("R3C2",FALSE)` and `INDIRECT("R3C2",0)` are 45, `INDIRECT("Positions[PV]")` is 250.5 and
`SUM(INDIRECT("A1#"))` over `=SEQUENCE(3)` is 6 (INDIRECT-023 to 026). R1C1 text — `a1` FALSE, 0, a blank or left
empty — naming an absolute cell, or a range of two such cells, is the Reference it names, with or without the Sheet's
own qualifier. A structured reference's text is the Linked Table's column, read as `Table[Column]` typed is: a table
still waiting for its data waits, even under `IFERROR`, and a table never declared, or a column it does not have,
names no place, `#REF!`. `A1#` is the Spill Range of the Formula in A1, read as `A1#` typed is, and `#REF!` where it
does not spill. **R1C1 text in a form no run has observed stays `#VALUE!`**: relative (`R[1]C[-1]`, `RC`), whole rows
or columns (`R3`, `C2`), a number with a leading 0, and structured or `#` text in R1C1 style (the two area passes of
the same day observed a form of each; the note below says what is read since, and what stays refused). INDIRECT-030 to
042 put the forms generalised from the observed ones to the next run. Excel reads R1C1 text in the letters of its display
language (`Z1S1` in a German Excel); ExSheet reads R and C in every culture, as its function names are English in
every culture, and the run's Excel was English. **A volatile Formula is computed once more after each layout of the
spills that changes something**, since what it computes a Reference to is no Dependent of the cells the layout
changed: a Reference `INDIRECT` or `OFFSET` computes into a Spill Range follows it when it is blocked or freed, as a
typed one does. An Anchor computed again with an array of the shape its layout blocked stays `#SPILL!`, so the
recalculation ends on a layout that changes nothing: a change that blocks or frees one spill computes each volatile
Formula twice, and one that moves no spill once.)*

*(2026-10-12, after the two area passes (ADR-0208's remedy): **R1C1 text is read as Excel was observed to read it,
and in the forms that follow from its answers by the rule they settle; a declared Linked Table's bare name stays
refused, by decision.** The first pass read, in C2, `INDIRECT("R[1]C[-1]",FALSE)` as 45, B3's Value,
`SUM(INDIRECT("R3",FALSE))` as 45 and `SUM(INDIRECT("C2",FALSE))` as 118.333 (INDIRECT-036 to 038). The second read
the forms those left open: `RC[-1]` from C2 is 1.333 and `R[2]C` from B1 is 45, so an R or a C with no number is the
Formula's own row or column, not every one (INDIRECT-044, 051, 066); `R[0]C[-1]` and `R[-0]C[-1]` are 1.333 and
`R[+1]C[-1]` is 45, so an offset is a signed number, 0 included (INDIRECT-063 to 065); `R03C2` is 45, so a number is
read with its leading 0s (INDIRECT-059); `R2C2:R3` and `R3:C2` are `#REF!`, so parts of two kinds joined name no place
(INDIRECT-058, 062); `R1C1#` is the Spill Range, as `A1#` is (INDIRECT-060); and `Positions[PV]` read in R1C1 style is
the column, as in A1 style (INDIRECT-061). INDIRECT-045 to 050, the forms the first pass's answers gave, agreed. What
follows is read until a run asks it (INDIRECT-067 to 072, 075 to 081, 093): R and C each take a number, an offset in
brackets or nothing, in any combination; a row part alone is a whole row, a column part alone a whole column; two
parts of the same kind joined by `:` name the rectangle they span, and two of different kinds name no place, whichever
comes first; a `#` after any one cell on its own is its Spill Range; another Sheet's name names no place, whatever follows it, as
it was observed to in A1 style (INDIRECT-008); a Linked Table's text is read alike whatever `a1` says. `R` alone is
therefore the Formula's whole row, `C` its whole column and `RC` its own cell, each a cycle. **Still `#VALUE!`**, where
the observed answers leave the readings apart: an offset past the Sheet's edge, which Excel reads as a place
(`R[-5]C[-1]` from C2 is 0, a blank cell, not `#REF!`, INDIRECT-052) though no run has told whether it wraps round to
the Sheet's far side or stops at the edge, since both cells are blank there; a row or column number 0; a row or column
number past the Sheet's edge (`R1048577C2`, `R2C16385`), which could name no place, as `XFE1` does in A1 style
(INDIRECT-017), or a place, as an offset past the edge does (2026-10-12, decided with the user: in R1C1 style a row or
column number past the Sheet's edge is refused with `#VALUE!` until Excel is asked, and INDIRECT-074 asks it; A1 text
past the edge stays `#REF!`, as `XFE1` was observed to be); and, in either style, a `#` after a range, a row or a
column, whatever the parts joined (`A1:A2#`, `A:A#`, `R2C2:R3C2#`, `R2C2:R3#`), which no run has observed in either
style and which could name no place, the first cell's Spill Range, the cells themselves or the first cell joined to
the second's Spill Range, `A1:(A2#)`; and, in either style, a `#` inside a join, after its first part (`A1#:A2`,
`R1C1#:R2C1`), which no run has observed either and which could name no place, the first cell's Spill Range joined to
the second part, `(A1#):A2`, as `A1#` is read, or the cells themselves with the `#` passed over (INDIRECT-073, 082 to
084, 090 to 092 and 094 to 099 ask them). Other A1 text
that goes on past its Reference names no place, as the intersection Excel was observed to refuse does (INDIRECT-021):
the engine's rule, which no case asks. **A declared Linked Table's bare name is `#VALUE!` by decision** (2026-10-12,
decided with the user: the bare name stays `#VALUE!`, in either style): Excel reads it as the table's data — over a table of two rows,
`COLUMNS(INDIRECT("Positions"))` is 2, `ROWS` 2, `INDEX(…,1,1)` the first row's first Value, `COUNTA` 4, and the name
as a Formula's result spills the data (INDIRECT-053 to 057), which also gives the first pass's `SUM`, 250.5
(INDIRECT-043) — but a Formula could then read the table by position, and
[ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)'s rule 2 reads a Linked Table by key, never by
position. The six cases hold Excel's answers beside the refusal, a difference by decision, and the refusal is a Known
limitation of `0.1.0` (ADR-0202). Typed, a table's bare name shows `#NAME?` and is refused, which no Formula
catches (2026-10-13, decided with the user; TABLE-024).)*
