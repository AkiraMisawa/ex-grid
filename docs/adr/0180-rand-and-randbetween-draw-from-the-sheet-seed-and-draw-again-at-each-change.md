# RAND and RANDBETWEEN draw from the Sheet Seed, and draw again at each change

*(Decided with the user on 2026-10-10, from the function catalogue's Decide list,
`docs/specs/exsheet-functions/spec.md`, ticket 15. What Excel was observed to do is in
`verification/2026-10-03-windows-functions/research.md`, "RAND and RANDBETWEEN".)*

[ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md) admits a
function when it gives the answer Excel gives. `RAND` has no answer to give. Microsoft documents "an
evenly distributed random real number greater than or equal to 0 and less than 1", a new one every
time the worksheet is calculated, and names no generator and no seed.
[ADR-0124](./0124-volatile-functions-are-recalculated-after-every-change.md) made volatile the
functions that answer from something their text does not name, and kept `RAND` and `RANDBETWEEN` out
for a decision of their own. A random Value is the one kind principle 6 cannot hold by itself: an
outcome never depends on timing, and an ordinary generator's next number depends on everything that
drew before it.

What Excel was observed to do (RAND-002/003/005, RANDBETWEEN-017, LET-017/018/031):

- 128 recalculations of `RAND()` all gave a number in [0,1).
- Two calls in one Formula gave two numbers. A call bound by `LET` was one number however often it
  was read.
- In automatic mode an edit to an unrelated cell drew again, and a Formula reading the draw
  (`=Z1*2`) followed it in the same recalculation. In manual mode the edit drew nothing until
  Calculate.

## The decision

**A draw is a function of four things and of nothing else:** the Sheet Seed, the cell whose Formula
calls the function, which call in that Formula it is (counted in the Formula's text), and how many
changes the Sheet has had since it was opened. The same Entries, Sheet Seed and count of changes give
the same draws, whatever order the engine computes cells in and whenever it computes them. No draw
comes from a stream that an earlier draw advanced, so the order of the work never shows in a Value,
as ADR-0124 already holds for a computed Reference.

- **What counts as Excel's answer is the contract it documents and was seen to keep.**
  - Every draw of `RAND` lies in [0,1), evenly distributed.
  - Two calls are two draws.
  - A `LET` binding is one draw however often it is read
    ([ADR-0181](./0181-let-binds-names-inside-one-formula-as-excel-accepts-them-from-the-keyboard.md)).
  - Everything that reads a draw in one recalculation reads the same Value.

  These are the admission tests. Excel's own numbers cannot be matched, since its generator is not
  documented, and are not asked for.
- **The draws change with a change, and only then.** A change is anything that changes what the
  Sheet's Formulas read, and each counts once:
  - an edit of an Entry, and an insertion or a deletion;
  - the undo or the redo of one of those;
  - a Linked Table's snapshot.

  A change of format, width or font changes no Value, recalculates nothing and draws nothing. The
  clock is not a change: the minute that moves `NOW` and the day that moves `TODAY` (ADR-0121,
  ADR-0124) recalculate without drawing again. This is Excel's automatic mode, less the clock, which
  Excel does not have.
- **Undo draws again.** An undo is a change like any other, so the Values it brings back are its
  Entries' with new draws, not the draws seen before. Excel's answer was not observed; the next
  Windows run asks it.
- **The engine takes the seed as data, as it takes the Sheet Day.** `ExSheet.Engine` never chooses a
  random number of its own: whoever runs it gives the Sheet its seed. A Sheet whose seed was never set
  answers `#GETTING_DATA` from `RAND` and `RANDBETWEEN`, as one whose day was never set answers it
  from `TODAY` (ADR-0121).
- **ExSheet keeps the seed.** It takes the Consumer's `Seed` when one is given: a sheet that must show
  the same draws whenever it is opened, such as a simulation someone else reviews. Otherwise it
  chooses a seed when it opens a Sheet Document. Either way the count of changes is 0 when a Sheet
  Document is opened.
- **The Sheet Document holds no draw and no seed.** It holds Entries
  ([ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)). A
  reopened document draws again, as Excel's reopened workbook does, unless the Consumer fixes the
  seed.
- **`RAND` and `RANDBETWEEN` are volatile** in ADR-0124's sense: every recalculation computes them,
  with what reads them. Between two changes they compute the same draws, so a recalculation by the
  clock moves nothing.

### `RANDBETWEEN`'s arguments

Its bounds are whole numbers, as Microsoft documents. Excel answers fractional bounds too, and was
seen to return a number past a fractional top (RANDBETWEEN-004/005/006 and the FOLLOWUP cases). Which
number it returns there is not something a guess may supply.

- **Refused by decision, with `#VALUE!`** (ADR-0047: a refused argument value gives an Error Value,
  never another answer):
  - a fractional bound;
  - a bound whose magnitude is above 2^53, past which a double no longer holds every whole number.
- **The rest is Excel's, as observed:**
  - a bottom above the top is `#NUM!`;
  - `TRUE`, and text that is not a number, are `#VALUE!`;
  - a numeric string is read as its number;
  - a blank Reference is 0;
  - an Error Value passes through;
  - both bounds can be drawn.

## What differs from Excel, by decision

- **When a draw is made.** Excel draws at every recalculation, and has a manual mode and F9 to
  recalculate on demand. ExSheet has neither (`docs/specs/exsheet/formula-dependencies.md`), and its
  recalculations by the clock draw nothing.
- **Where the draw comes from.** Excel's draws come from its own generator; ExSheet's from the Sheet
  Seed.
- **`RANDBETWEEN`'s bounds.** Fractional bounds, and bounds past 2^53, are refused.

*(2026-10-11, decided with the user: renaming the Sheet is not a change that draws again; it is not in
the list above. It still recalculates volatile Formulas, since `INDIRECT`'s text can name the Sheet, and
those draws come out the same. Whether Excel draws again on a rename is asked on the sign-off's Windows
run, and this follows Excel's answer.)*

## Considered options

- **An unseeded generator inside the engine.** Rejected. The Values would depend on the order cells
  were computed in and on when the Sheet was opened, and `ExSheet.Engine` would no longer give the
  same Values from the same Entries (SH-17).
- **One seeded stream shared by every call.** Rejected for the same reason: each draw would depend on
  how many draws the engine happened to make before it.
- **Drawing in the component when a cell is painted.** Rejected. A Value comes from one completed
  recalculation (ADR-0047), and the grid neither holds nor executes.
- **Undo brings back the draws it undoes.** Possible without storing a Value, if each step kept its
  count of changes. Not taken while Excel's answer is unknown.
- **The document stores the seed or the draws.** Rejected. The draws are Values, which ADR-0048 keeps
  out of the document. A seed in the document would need a new Sheet Document version, and a fixed
  seed is already the Consumer's to give.
- **Drawing again on the clock as well, as volatile `NOW` moves.** Rejected. A random number on
  screen would change every minute with nothing done, which is the timing principle 6 refuses.

## Consequences

- `RAND` and `RANDBETWEEN` become Ready in the catalogue, and ticket 15 builds them.
- SH-17 holds for a Sheet that draws, when the engine is given the same Sheet Seed and count of
  changes.
- Glossary: **Sheet Seed**.
- The next Windows run asks Excel what an undo does to a draw.
