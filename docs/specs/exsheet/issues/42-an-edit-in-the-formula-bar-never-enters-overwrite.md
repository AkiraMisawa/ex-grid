# 42: An edit in the Formula Bar never enters Overwrite

Status: ready-for-agent

**What to build:** ADR-0051's note of 2026-09-30, "An edit in the Formula Bar never enters
Overwrite". Found by Part B of the eighth Windows run
(`verification/2026-09-30-windows-8/reference-outlines.md`, case `7k`): `=A1+B1` typed into the
Formula Bar, then `Home`, committed the Formula and moved the Focus to column A. Typing the `A` had
ended Point in Overwrite. Excel's Formula Bar is always in Edit.

**Blocked by:** None (can start immediately)

- [x] While the edit's surface is the Formula Bar, typing that ends Point returns to Caret, not
      Overwrite (`ExGrid.FormulaEntry.cs`, where typing ends Point) (ED-29)
- [x] An edit that moves from the cell into the bar (`OnFormulaBarFocusAsync` with an edit open)
      goes into Caret; one that moves back into the cell keeps the mode it has (ED-29)
- [ ] `Home`, `End`, ← and → in the bar move the caret and commit nothing; Enter, Tab and Escape keep
      their meanings; the key listener's gate follows the mode the core tells it (ED-29). *Open:
      every path above now holds, but F2 in the bar still enters Overwrite where no Reference can
      go, and `Home` then commits. See the comment below*
- [x] Nothing changes for an edit in the cell: typing onto a cell still opens Overwrite, and the arrows
      still commit and move there (ADR-0012)
- [x] Layer 2: the mode after typing in the bar, after a press into the bar mid-edit, and in the cell
      (ED-29)
- [ ] Layer 3 on `/sheet` under both Chromes: `=A1+B1` typed into the bar, then `Home`, →, three
      Deletes: the bar holds `=B1`, the edit is open, and the Focus has not moved (ED-29). Write it;
      the orchestrator runs it. *Written, not run*

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
  on 4179010 and on this branch alike: after `=A1+B1` in the bar the gate is told `caret`, and
  `Home` commits nothing. After F2 it is told `overwrite`, and `Home` commits `=A1+B1` and moves
  the Focus from B1 to A1. So:
  - **Proposal, needing a decision.** ADR-0051's note says the bar is "never in Overwrite", but also
    that F2 in the bar "is left as it is". Left as it is, F2 enters Overwrite. The proposal is
    that in the bar F2 moves between Caret and Point only. Where no Reference can go, it changes
    nothing, which is what the run saw. Not built.
  - **The ticket's layer-3 case alone does not catch the defect.** `=A1+B1` typed into the bar,
    `Home`, →, three Deletes, is expected to pass on 4179010 too. The test written for it
    therefore runs two more ways into the bar, both of which fail on 4179010 at layer 2. One points
    from the bar with a press on A1 and types on. The other begins in the cell and presses into
    the bar.
- **Layer 2** (`tests/ExGrid.Components/FormulaBarModeTests.cs`, nine tests). Seven of them fail
  on 4179010. The two that pass there are about the cell's unchanged behaviour and about Point
  kept when the core hands the keyboard back.
- **Layer 3, written, not run**: `ED-29: =A1+B1 … (builtin|mud Chrome)` in
  `tests/ExGrid.Browser/declarations.spec.mjs`, three ways under each Chrome, from D10.
