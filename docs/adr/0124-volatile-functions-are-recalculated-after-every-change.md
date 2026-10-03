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
