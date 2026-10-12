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
- [x] Which changes count, each once however many steps it takes:
      - they do: an edit, an insertion or a deletion, the undo or redo of one, a Linked Table's
        snapshot, and a Linked Table's declaration, or one that replaces it (ADR-0180's second note
        of 2026-10-11);
      - they do not: a recalculation by the clock, a change of format, and a rename (ADR-0180's
        first note of 2026-10-11)
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

2026-10-11, after review: a prerender chooses no seed. Without the Consumer's `Seed`, the instance a
circuit or the browser runs after a prerender chose another, so every draw on the page changed when it
became interactive, with nothing done. The prerender's draws now wait with `#GETTING_DATA`, as `TODAY`
waits for the browser's zone, and a `Seed` given draws there as well (`SeedWiringTests`). The Formulas
page and both READMEs say so, and no longer call an undo's new draw a difference from Excel: what
Excel's undo does has not been observed. Still not counted as a change, and left for a decision: a
rename, and a Linked Table's declaration.

2026-10-11, with ADR-0180's note of that date (decided with the user: renaming the Sheet is not a
change): a rename recalculates the volatile Formulas, since `INDIRECT`'s text can name the Sheet, and
draws nothing, nor do its undo and redo (`ADR0180_a_rename_draws_nothing`); the Formulas page and both
READMEs say so. The Status is `done`: every box is ticked. Two questions are ADR-0180's for the sign-off's
Windows run, and are no corpus case, since the oracle cannot undo through COM: what Excel's undo does to a
draw, and whether a rename draws again. Still left for a decision, as the comment above says: whether a
Linked Table's declaration is a change; ADR-0180's list does not name it, and it draws nothing today.

2026-10-11, decided with the user (ADR-0180's second note of that date): a Linked Table's declaration,
and one that replaces it, is a change and draws again; the same declaration again draws nothing
(`ADR0180_a_linked_tables_declaration_draws_again`). The Formulas page, both READMEs and the Change Note
say so. The seventeenth Windows run asks Excel whether a refresh that brings the same data draws again.

2026-10-11, after the second review: a paste ExSheet wrote in several engine steps — typed fields with
exact numbers, and its own copy over a Selection of several ranges — counted a change for each step, and
so did its undo and redo, though `Changes` says an operation counts once. Each is now one engine edit
(`SheetEdit.PasteText(fields, entries)` and `SheetEdit.Paste(block, targets)`): one step on the undo
stack and one change (`ADR0180_a_paste_in_several_parts_counts_once`, and in `SeedWiringTests` the
pastes, their undo and redo, and a refused paste, which draws nothing). Three questions, not two, are
left for the sign-off's Windows run by hand: what an undo does to a draw, whether a rename draws again,
and whether a refresh that brings the same data draws again.

2026-10-12, from the seventeenth Windows run (`verification/2026-10-11-windows-17/report.md`, Part B b1
to b3; `records/part-b.jsonl`), which asked Excel the three questions left above:
- **An undo draws again (b1): agrees.** Every Ctrl+Z and Ctrl+Y, twice each, drew a number Excel had not
  shown before, never the draw before the edit nor the one after it. ExSheet already draws again on each;
  `ADR0180_an_undo_and_a_redo_draw_again` (it was `ADR0180_an_undo_draws_again`) now pins redo as well as
  undo, two rounds each, every draw new.
- **A rename draws again (b2): followed**, as ADR-0180's first note of 2026-10-11 said it would. Excel drew
  again on the rename, offered its undo, and drew again when Ctrl+Z put the name back. A rename is now a
  change, and so are its undo and redo, each counted once as `_operationDepth` already ensures
  (`Sheet.ApplyRename` and `Sheet.UndoRename`); a rename to the name the Sheet has, and its undo and redo,
  change nothing. `ADR0180_a_rename_draws_nothing` became `ADR0180_a_rename_draws_again` (INDIRECT by the
  Sheet's name still reads the right place), with `ADR0180_a_rename_to_the_same_name_draws_nothing`, the
  rename's three counts in `ADR0180_each_change_counts_once`, and
  `SeedWiringTests.ADR0180_a_rename_its_undo_and_its_redo_draw_again` through the component.
- **A refresh that brings the same data draws again (b3): agrees.** Excel's Refresh All drew again each
  time, with the file unchanged and changed alike. ExSheet's counterpart is `PushLinkedTable`, which
  already counts as a change whatever its rows; `ADR0180_a_snapshot_draws_again_whatever_its_rows` and a
  second push of the same rows in `ADR0180_each_change_counts_once` pin it. The same declaration again
  still draws nothing: a refresh brings data, it does not declare its query again.

The Change Note `sheet-seed.md` now names the rename among the changes: `RAND` is in no published
version yet, so its Added note is what changes, not a Behaviour note. The ADR-0180 notes, both READMEs,
the Formulas page and SH-66 are proposed to the orchestrator in
`docs/agents/proposals/run17-draws-rename.md`. Nothing is left open here.
