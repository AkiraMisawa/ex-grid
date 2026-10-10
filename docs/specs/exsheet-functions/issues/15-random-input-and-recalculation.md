# 15: RAND and RANDBETWEEN, drawn from the Sheet Seed

Status: ready-for-agent

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
- [ ] The engine: the Sheet's seed as data (`#GETTING_DATA` until one is set, as `TODAY` is with no
      day), the count of changes, and each draw computed from its four inputs, with no shared stream
- [ ] Which changes count:
      - they do: an edit, an insertion or a deletion, the undo or redo of one, and a Linked Table's
        snapshot;
      - they do not: a recalculation by the clock, and a change of format
- [ ] `RAND` and `RANDBETWEEN` in the volatile set (ADR-0124)
- [ ] `RANDBETWEEN`'s arguments: ADR-0180's refusals, and the answers observed in Excel
- [ ] ExSheet:
      - a `Seed` parameter;
      - otherwise, a seed chosen when a Sheet Document is opened;
      - the count of changes set back to 0 on opening;
      - nothing recorded in the Sheet Document
- [ ] Tests of the contract, with a fixed seed (SH-66):
      - draws in range;
      - two calls, two draws;
      - the same draws for the same seed and count, whatever the order of computation;
      - a new draw at each change, and none by the clock;
      - SH-17 with the seed and count given
- [ ] The catalogue rows Supported; the Docs Site's Formulas page says what differs from Excel, and
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
