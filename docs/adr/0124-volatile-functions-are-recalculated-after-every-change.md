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
or columns (`R3`, `C2`), a number with a leading 0, and structured or `#` text in R1C1 style. INDIRECT-030 to 042 put
the forms generalised from the observed ones to the next run. Excel reads R1C1 text in the letters of its display
language (`Z1S1` in a German Excel); ExSheet reads R and C in every culture, as its function names are English in
every culture, and the run's Excel was English. **A volatile Formula is computed once more after each layout of the
spills that changes something**, since what it computes a Reference to is no Dependent of the cells the layout
changed: a Reference `INDIRECT` or `OFFSET` computes into a Spill Range follows it when it is blocked or freed, as a
typed one does. An Anchor computed again with an array of the shape its layout blocked stays `#SPILL!`, so the
recalculation ends on a layout that changes nothing: a change that blocks or frees one spill computes each volatile
Formula twice, and one that moves no spill once.)*
