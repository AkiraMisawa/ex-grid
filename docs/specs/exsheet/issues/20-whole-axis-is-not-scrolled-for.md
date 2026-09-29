# 20: An axis the range spans end to end is not scrolled for

Status: done

**What to build:** ADR-0052, "What the user's run settled" (2026-09-29). With the view at the top,
a Column Heading click puts the Focus on row 1, the range's top edge, so the Extent is the column's
last row. Shift+→ then moves the Extent to the last row of the next column, and the reveal scrolls
the Sheet to its bottom. Excel and Google Sheets both leave the view where it was. **While
extending, keep the Extent in view only on an axis the range holding the Focus does not span end
to end**, judged on the range after the move. The Extent and the Selection are unchanged: this is
a rule about the view.

The same state is reached in a plain ExGrid by Shift+click on a header or Ctrl+Space on row 1, and
on the other axis by a Row Heading click with the view at the left (Shift+↓ scrolls to the last
column today). All of them take the rule.

**Blocked by:** None (can start immediately)

- [x] Shift+→ / Shift+← over whole columns never scrolls vertically; Shift+↓ / Shift+↑ over whole
      rows never scrolls horizontally; over the whole grid neither (SR-2c)
- [x] Shift+↑ from C:C selected on row 1 leaves C1:C(last − 1) and scrolls to show the Extent, as
      Excel does (SR-2c)
- [x] Every extension that reveals the Extent takes the rule: Shift+arrow, Ctrl+Shift+arrow,
      Shift+Home/End, Shift+PageUp/PageDown, Shift+click, and a drag
- [x] The Focus reveal (non-extending moves), Ctrl+Backspace and the Consumer's placement are
      unchanged
- [x] Layer 3: with the view at the top, a Column Heading click then Shift+→ leaves `scrollTop`
      where it was, on both hosts

## Comments

Where it goes wrong today: `GridSelection.ExtentOf` takes the corner opposite the Focus, which for a
whole column with the Focus on row 1 is the last row; the reveal in `ExGrid.razor` (the
`_revealTarget` path) then reveals both axes of that cell. The fix belongs in the reveal, not in
`ExtentOf`: Shift+↑ from the same state must still move the Extent from the last row.

2026-09-29, built. The rule is in the one reveal every keyboard extension goes through
(`RevealFocusIfNeededAsync` in `ExGrid.razor`): the reveal target now carries the range being
extended (`ExtentReveal` in `ExGrid.Pointing.cs`), and an axis that range spans end to end, after
the move, keeps the offset the scroller is going to. The pointing outline's reveal carries its own
range and takes the same rule, as its comment says it is kept on screen "as the Extent is".
Shift+click and a drag reveal nothing: the pointer is on the Extent, and a cell drag's edge band
follows the pointer. The Heading drag's edge band is ticket 21's (DC-43). Layer 2:
`tests/ExGrid.Components/WholeAxisRevealTests.cs` (13 tests; 8 were red before the change).

Two readings were needed, and neither is a new rule:

- Shift+Home over a row that it makes whole still puts the Viewport's left edge at the start.
  ADR-0012 separates "go to the beginning" from "make this cell visible", and names Shift+Home as
  the first; the rule here is about the second. `GoToStartTests.Shift_home_goes_to_the_start_too`
  pins it.
- Page turns under a pager are left as ADR-0015 has them. Applying the rule to them would stop
  Ctrl+Shift+↓ from the first row turning to the last page, which
  `PagerTests.Shift_arrow_turns_the_page_and_continues` pins. So under a pager, whole columns
  followed by Shift+→ still turn to the page of the Extent's row: open, raised with the
  orchestrator.

Not observed in Excel, and applied as ADR-0052 is written ("judged on the range after the move"):
a move that itself makes the range span an axis holds that axis too. Ctrl+Shift+↓ from row 1 of an
empty column, or Ctrl+Shift+→ from a whole column to the whole grid, scrolls nothing.

Layer 3 is written and not yet run: `tests/ExGrid.Browser/headings.spec.mjs` (the three `SR-2c:`
tests, on /sheet) and `tests/ExGrid.Browser/sizing.spec.mjs` (the two `(SR-2c, ADR-0052)` tests,
on a plain ExGrid). The last box waits for that run.

2026-09-29, layer 3 run: `headings.spec.mjs`, `sizing.spec.mjs` and `sheet.spec.mjs` pass in Chrome
on macOS against both hosts, WebAssembly and Server (46 of 46 on each; `headings.spec.mjs` three
times over on each as well). Edge, and Linux, are CI's. The first run, by the orchestrator, failed
the `SR-2c: Shift+↑ …` test on the test's own last line: it read the Focus from
`aria-activedescendant`, which is cleared while the Focus is off screen (ADR-0033); it reads the
Name Box now. Done as specified; the two open points above stand with the orchestrator, and a
refinement of SR-2c from them would come as its own change.

2026-09-29, the user's observation (orchestrator): on an empty sheet, Excel's Ctrl+Shift+↓ from A1
selects the whole column with A1 active and does not scroll down. "Judged on the range after the
move" is therefore Excel's rule, as built, and the proposed "before and after" variant is dropped
(ADR-0052). Whether page turns under a pager take the rule is still with the user.

2026-09-29, the pager, as decided (ADR-0015 and ADR-0052; SR-2c): a page turn is the pager's scroll
and takes the same rule. The keyboard's page turn in `ExGrid.razor` now skips an extension that
leaves the range spanning every row, so Shift+→ over whole columns turns no page and Ctrl+Shift+↓
from the first row stays on its page; a Shift+arrow that leaves the range short of the first or
last row still turns it. `PagerTests.Shift_arrow_turns_the_page_and_continues` is rewritten to start
from row 1, where it still holds, and three tests are added:
`Ctrl_shift_down_from_the_first_row_stays_on_its_page` and
`Shift_right_over_whole_columns_turns_no_page` (both red before the change), and
`Shift_up_from_a_whole_column_turns_to_the_extents_page`. No DemoHost page declares a `PageSize`,
so no layer 3 spec reaches the pager; FN-16's pager is verified on layer 2.
