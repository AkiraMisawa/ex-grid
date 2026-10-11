# 42: An edit in the Formula Bar never enters Overwrite

Status: done

**What to build:** ADR-0051's note of 2026-09-30, "An edit in the Formula Bar never enters
Overwrite". Found by Part B of the eighth Windows run
(`verification/2026-09-30-windows-8/reference-outlines.md`, case `7k`): `=A1+B1` typed into the
Formula Bar, then F2 and `Home`, committed the Formula and moved the Focus to column A. F2 had taken
the edit from Caret to Overwrite. Excel's Formula Bar is always in Edit. *(Corrected when built, as
ADR-0051's note was: this first said typing had ended Point in Overwrite.)*

**Blocked by:** None (can start immediately)

- [x] While the edit's surface is the Formula Bar, typing that ends Point returns to Caret, not
      Overwrite (`ExGrid.FormulaEntry.cs`, where typing ends Point) (ED-29)
- [x] An edit that moves from the cell into the bar (`OnFormulaBarFocusAsync` with an edit open)
      goes into Caret; one that moves back into the cell keeps the mode it has (ED-29)
- [x] F2 in the bar moves only between Caret and Point: where no Reference can go it changes
      nothing; F2 in the cell still goes from Caret to Overwrite there (ADR-0051, 2026-09-30,
      decided with the user when this ticket was built; ED-29)
- [x] `Home`, `End`, ← and → in the bar move the caret and commit nothing; Enter, Tab and Escape keep
      their meanings; the key listener's gate follows the mode the core tells it (ED-29)
- [x] Nothing changes for an edit in the cell: typing onto a cell still opens Overwrite, and the arrows
      still commit and move there (ADR-0012)
- [x] Layer 2: the mode after typing in the bar, after a press into the bar mid-edit, after F2 in
      the bar, and in the cell (ED-29)
- [x] Layer 3 on `/sheet` under both Chromes: `=A1+B1` typed into the bar, F2, `Home`, →, three
      Deletes (case `7k`); the same after pointing from the bar, and after an edit begun in the cell
      is pressed into the bar: the bar holds `=B1`, the edit is open, and the Focus has not moved
      (ED-29). Write it; the orchestrator runs it. *Written, not run*

## Comments

*(2026-09-30, built.)*

- **Where typing ends Point.** Point gives way to Overwrite in the cell and to Caret in the Formula
  Bar (`ModeAfterPointing`, `ExGrid.FormulaEntry.cs`). F4's rewrite that ends pointing "as typing
  ends it" (`ExGrid.Pointing.cs`) uses the same rule, since the bar is never in Overwrite.
- **The move into the bar.** `OnFormulaBarFocusAsync` with an edit open puts an edit that comes in
  from the cell into Caret, from Overwrite or from Point. The Point outline goes, as a caret moved
  away from its Reference ends it. Overwrite never stands in the bar. Point already in the bar
  stays. That focus is the core itself handing the keyboard back to the bar after a press on a
  cell pointed (ED-26, `edit-stands.spec.mjs`). Ending Point there would have broken the next
  press's re-pointing. An edit that moves back into the cell keeps its mode, since nothing on that
  path changes it.
- **A key the gate took under the old mode.** On a circuit, a key typed straight after the press
  into the bar is gated as Overwrite's until the gate hears Caret. The listener holds keys behind
  a bar press only when no edit is open. Such a key already did nothing in Caret where formula
  entry is declared. It now does nothing wherever the bar is used (`OnEditingKeyAsync`, the Caret
  guard, no longer asks for completion or Point). Without that, a grid with a Formula Bar and no
  formula entry would have committed on it. Its default was prevented, so that one `Home` moves
  no caret. Holding keys behind a bar press mid-edit as well would fix that, but it is a change to
  the listener, and none was made (ADR-0021).
- **What case `7k` actually hit was F2, not typing.** Typing `=A1+B1` into the bar never ends
  Point: the press into the bar opens Caret, and nothing typed there enters Point. The run's keys
  were `=A1+B1`, **F2**, `Home`. From Caret, F2 goes to Point where a Reference can go and to
  Overwrite everywhere else (ADR-0010, `OnEditingKeyAsync`). After `B1` no Reference can go, so
  F2 put the bar into Overwrite, and `Home` committed. A layer-2 probe, not kept, gave this result
  on 4179010 and on this branch before the F2 rule below: after `=A1+B1` in the bar the gate is told `caret`, and
  `Home` commits nothing. After F2 it is told `overwrite`, and `Home` commits `=A1+B1` and moves
  the Focus from B1 to A1. So:
  - ADR-0051's note said the bar is "never in Overwrite", but also that F2 in the bar "is left as it
    is". Left as it is, F2 enters Overwrite. Put to the user, who decided that F2 in the bar moves
    only between Caret and Point. ADR-0051's note and ED-29 were corrected (2629c97).
  - The first layer-3 case, `=A1+B1` typed into the bar, `Home`, →, three Deletes, without F2, is
    expected to pass on 4179010 too. ED-29 now takes 7k's own keys, F2 included. The case without
    F2 is kept beside it.

*(2026-09-30, the F2 rule built.)* One arm in the F2 switch in `OnEditingKeyAsync`: from Caret in
the bar, where no Reference can go, the mode stays Caret. F2 still asks for the keyboard, and the
gate is told the unchanged mode. Point → Caret and Caret → Point are as before, so pointing from
the bar with F2 (DC-19, DC-28) is untouched. The surface is the core's record. That record hears a
press back into the cell only when text is next typed there. So F2 in the cell, straight after
such a press and before anything is typed, stays in Caret, as in the bar. That is accepted, and
said in the code: it errs toward Caret, and never commits.

- **Layer 2** (`tests/ExGrid.Components/FormulaBarModeTests.cs`, twelve tests). Eight fail on
  4179010's component source. The four that pass there pin what did not change: the cell's
  Overwrite, F2 in the cell, F2 once the edit is typed in the cell again, and Point kept when the
  core hands the keyboard back.
- **Layer 3, written, not run**: `ED-29: =A1+B1 … (builtin|mud Chrome)` in
  `tests/ExGrid.Browser/declarations.spec.mjs`, from D10, under each Chrome. Four ways into the bar:
  typed there, typed there then F2 (case `7k`), pointed from there and typed on, and begun in the
  cell then pressed into the bar.

2026-09-30, after the tenth Windows run (group 4): Excel's F2 takes its Formula Bar from Edit to
Enter, and Home, → and ↓ then enter the Formula and move. The user chose Excel's behaviour (Q47), so
the F2 arm that kept the bar in Caret is removed: F2 in the bar does what it does in the cell. The
layer 2 test for it became three (cases 20, 23, 24), and ED-29's case `7k` now expects the Formula
entered into D10 and the Focus on A10. Typing in the bar, and an edit carried into it, still go into
Caret, as Excel's bar stays in Edit (cases 14–19). The ticket keeps its file name; its title now reads
past what the tenth run corrected.

*(2026-10-10, backlog cleanup.)* Status set to done, and the last box ticked: written then, run in
CI since. `declarations.spec.mjs` runs "ED-29: =A1+B1 {typed into the bar | pointed from the bar,
then typed on | begun in the cell, then pressed into the bar}, then Home, → and three Deletes" under
both Chromes, the same edit on a 150 ms round trip, and case 7k. Case 7k asserts the tenth Windows
run's reversal (above), not the box's first wording.
