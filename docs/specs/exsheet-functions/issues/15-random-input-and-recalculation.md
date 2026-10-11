# 15: RAND and RANDBETWEEN, drawn from the Sheet Seed

Status: done

**What to build:** `RAND` and `RANDBETWEEN` under
[ADR-0180](../../../adr/0180-rand-and-randbetween-draw-from-the-sheet-seed-and-draw-again-at-each-change.md).
Each draw is a function of four things: the Sheet Seed, the cell, the call's place in its Formula,
and the count of changes since the Sheet was opened. A change draws again; the clock never does.
The engine takes its seed as data, ExSheet takes a `Seed` parameter, and `RANDBETWEEN` has its
refusals.

**Blocked by:** none.

*(Until 2026-10-10 this ticket was "Decide reproducible random input and recalculation",
`needs-info`. The decision is ADR-0180, and the boxes below it are the build.)*

- [x] Retain the October 3 repeated calculation, dependency and lifecycle observations.
- [x] Decide who supplies randomness and how the same Sheet state can be reproduced (ADR-0180)
- [x] Decide when random results change and how dependents recalculate deterministically (ADR-0180)
- [x] Decide what a Sheet Document and undo/redo preserve (ADR-0180)
- [x] Record the decision before introducing hidden random state or admitting the functions (ADR-0180)
- [x] The engine: the Sheet's seed as data (`#GETTING_DATA` until one is set, as `TODAY` is with no
      day), the count of changes, and each draw computed from its four inputs, with no shared stream
- [x] Which changes count:
      - they do: an edit, an insertion or a deletion, the undo or redo of one, and a Linked Table's
        snapshot;
      - they do not: a recalculation by the clock, and a change of format
- [x] `RAND` and `RANDBETWEEN` in the volatile set (ADR-0124)
- [x] `RANDBETWEEN`'s arguments: ADR-0180's refusals, and the answers observed in Excel
- [x] ExSheet:
      - a `Seed` parameter;
      - otherwise, a seed chosen when a Sheet Document is opened;
      - the count of changes set back to 0 on opening;
      - nothing recorded in the Sheet Document
- [x] Tests of the contract, with a fixed seed (SH-66):
      - draws in range;
      - two calls, two draws;
      - the same draws for the same seed and count, whatever the order of computation;
      - a new draw at each change, and none by the clock;
      - SH-17 with the seed and count given
- [x] The catalogue rows Supported; the Docs Site's Formulas page says what differs from Excel, and
      an Example runs `Seed` (CLAUDE.md, "The Docs Site moves with the change")

## Comments

2026-10-03: both functions remain Decide and undeclared. A generic RNG in the evaluator would
silently choose lifecycle and persistence semantics. The observations constrain a future ADR;
they do not authorize timing-dependent or unreproducible behavior.

2026-10-10, decided with the user: ADR-0180. The Status is `ready-for-agent`. Two things are left for
others:
- the test that a `LET` binding is one draw needs ticket 14 too, and whichever lands second writes
  it;
- the next Windows run asks Excel what an undo does to a draw.

2026-10-11: built. A draw is SplitMix64's finaliser over the seed, the cell's row and column, the call's
place among the Formula's draws (counted in its text by the parser) and `Sheet.Changes`; `RAND` takes its
top 53 bits, and `RANDBETWEEN` scales it to the count of whole numbers between the bounds, mixing again
on the rare draw that would favour some, so each is evenly spread and every draw reproducible. The engine
takes `Sheet.SetSeed(seed)` and `SetSeed(seed, changes)`; without a seed every Formula that draws waits,
whichever branch it takes. An operation counts once however many steps it takes (`Sheet.Do`, an undo, a
redo), and a change now recalculates the volatile Formulas even when nothing else needs it (a snapshot of
a table nobody reads, an insertion that rewrote nothing). ExSheet's `Seed` parameter, a seed chosen with
`Random.Shared` when a document is opened otherwise, the count back to 0 there, and nothing in the
document. `RandomTests` and `SeedWiringTests` are the contract (SH-66), SH-17 included at a seed and count;
`ExcelCases/rand.json` and `randbetween.json` hold the observations with one outcome, the refused bounds
with Excel's outcome beside them where it had one (RANDBETWEEN-FOLLOWUP-007 agrees: a Formula's constant
1.000000000000001 is read at 15 digits, as 1); the samples are the contract tests'. The test that a `LET`
binding is one draw is here (`ADR0180_a_let_binding_is_one_draw`, and LET-017, LET-018 in the corpus), as
this ticket landed second. The Formulas page has a seeded Example (`SeededDraws`), run once on the Docs Site
with its MudBlazor twin: the same draws after a reload, new ones after an edit. Left for others: the next
Windows run asks Excel what an undo does to a draw. Not counted, as ADR-0180's list does not name it: a
rename, though it recalculates the volatile Formulas, since `INDIRECT`'s text can name the Sheet. ADR-0047's
dated addition naming the functions is left to the orchestrator.
