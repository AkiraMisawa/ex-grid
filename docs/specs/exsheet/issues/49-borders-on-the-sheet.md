# 49: Borders on the Sheet, as Excel draws them

Status: needs-info

**What to build:** the Sheet's Borders, drawn through ADR-0050 item 15, as [ADR-0071](../../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) says.

**Blocked by:** None (47 and 57 are in). The eleventh Windows run's group 3 is in `verification/2026-10-01-windows-excel-11/cell-format.md`;
ticket 57 makes the engine keep the line between two cells as Excel does, and ExSheet answers the
core's question of which line to draw by its rule (the upper or left cell's, where both record one).

- [x] ExSheet answers each cell's Border sides from the engine.
- [x] A Border on a whole row or column is drawn on cells that hold nothing, so a change to a level
      repaints every painted row it covers, as ticket 48 does for Fills.
  - An edge set on a row's top shows on the bottom of the row above, and a bottom on the row below.
    `SheetChange.Rows` names the row across a changed edge for cell records (ticket 57), but a level
    change still lists only rows that hold a cell. So the repaint after a level change reaches one row
    further on each side.
- [x] ExSheet answers the core's "which line to draw" from `Sheet.GetBorders`, which gives the edge
      as shown (ticket 57): where both cells record a line the upper or left cell's, otherwise whichever
      records one. The two records of an edge can differ after a paste, a fill or a deletion, so the
      core must not draw either side's record on its own.
- [x] A Fill covers the gridlines at its cell's edges, as the run observed (cases 4–6).
- [x] The thirteen line styles match case 9's table at 100% and 150%: every 1-px style on the
      gridline; medium and the medium dashes on the gridline and the pixel above it; thick on the
      gridline and a pixel each side; double as two 1-px lines either side of the gridline with the
      gridline's pixel white; the dash and dot patterns as tabled (dashes 8 px at 100%, 9 at 150%).
- [ ] Inside the Selection, borders stay drawn over its shade; the Selection's outline covers the
      outer ones (case 11).
- [x] Rows keep their one height where Excel would raise them for a medium or a thick line (ADR-0071).
- [x] A line on column A's left edge lies under the Row Headings' edge, as Excel draws it (run 12,
      case 14).
- [ ] Layer 3 beside Excel's screenshots, which is Part C of the run (SH-46).

## Comments

2026-10-01, agent cf-48.

Every box but two is built and passing:
- Case 11's top and left need a decision on ADR-0008's outline.
- Part C is a run by hand on Windows; the DemoHost now shows its cases.

The status is `needs-info` for the decision.

### What was built

- **The sides** (`SheetAppearance.Of`). Each cell declares its four sides as `Sheet.GetBorders`
  shows them: the edge as shown, which is the upper or left cell's where both record a line,
  otherwise whichever records one.
  - Both cells of an edge therefore name the same line, and the core never draws either cell's own
    record by itself.
  - The engine's line styles map to the core's by name. An Automatic line is Excel's black.
  - ExSheet also declares `EdgeBorder`, answering by the same rule (the upper or left line). With
    the edges as shown, the core asks it only of a row whose answer is out of date.
- **Repainting.** Ticket 48's readings carry the Borders.
  - A cell record's top or bottom is named with the row across it (ticket 57).
  - A level change, and an insertion or deletion, start a new reading
    (`SheetChange.ReformatsUnnamedRows`). The grid then reads every painted row again and repaints
    those whose lines moved. That reaches the row on each side of a whole row: the row above holds
    its top line, and the row below holds the pixel of a thick bottom.
  - The insertion or deletion is the case `Rows` misses: deleting the row under a line takes it off
    an empty row above.
- **A Fill covers its gridlines.** The core already painted this (ticket 47). It shows now that the
  Sheet has Excel's gridlines (ticket 48), and layer 3 reads it.
- **150%: the core now puts the rows on a device pixel.**
  - At 150% the Sheet's 1,048,576 rows are compressed under the Layout Ceiling (ADR-0053), so a
    scrolled slice is translated by a fraction of a pixel, for example `translateY(19.249px)`.
    `.ex-viewport` is a composited layer (`will-change: transform`), so it was resampled there, and
    every line drawn in device pixels blurred across two. Ticket 47's 150% check read the top of a
    small grid, where the offset is 0.
  - `ExGrid.ViewportStyle` now emits `translateY(round(nearest, Npx, var(--ex-dp, 1px)))`. The
    offset is still computed in C#; CSS only rounds it to a device pixel (`--ex-dp`, ticket 47's
    token).
  - The rows, the selection layers and the editor stand in that layer, so they still move together.
    A popover placed from the unrounded offset lies less than a device pixel away.
  - On the Sheet at 150%, scrolled to each line: 18 of 29 failed without the rounding, and 29 of 29
    pass with it.
  - The compressed grids' text is now on a device pixel too.
  - Two layer-2 tests read the old string form (`VirtualisationTests`, `LayoutCeilingTests`), and
    now read the new one. **The scroll and compression specs (BIG-1, VZ-15, far reveal) were not
    run locally; that is CI's.**
- **The DemoHost's cases** (`SheetCases`, `/sheet?case=`) now hold group 3 for Part C:
  - 7 (its first setting), 8, 9, 10, 11 and 12 (set up; Part C presses the keys), and 13 and 14
    (keys only);
  - the twelfth run's case 1 (`12-1`: the line both cells record) and case 14 (`12-14`: an outline
    on row 3);
  - `lines`: case 9's styles on B's bottoms and D's rights, with their names in column A.

### Tests

- **Layer 2**: `SheetBorderTests`, 23 tests:
  - the style mapping;
  - a line declared on both cells of its edge;
  - case 12-1, where the left cell's line is drawn and the other not at all;
  - `EdgeBorder`'s rule;
  - a line on a whole row on empty cells;
  - render counts: a cell's thick line repaints its row and the next, and no other; a whole row's
    repaints one row further each side, and no other;
  - whole columns drawn on every painted row, and undone;
  - a deletion taking a line off the empty row above;
  - an insertion moving a line with its cell.
- **Layers 1 and 2 in full**: ExGrid.Tests 871, ExSheet.Engine.Tests 2316, ExGrid.MudBlazor.Tests
  88, ExGrid.Components 1248 (one skipped), ExSheet.MudBlazor.Tests 37, ExSheet.Components.Tests
  577.
- **Layer 3**, headless on this Mac, WebAssembly, `sheet-borders.spec.mjs`:
  - `chrome`: 34 passed, 1 `fixme` skipped.
  - `chrome-150` (a real `--force-device-scale-factor=1.5`): 29 passed (the DC-59 tests).
  - It reads the thirteen styles on a bottom and a right edge, scrolled to each, against case 9's
    table: 1-px styles on the gridline; medium and the medium dashes on it and the pixel above;
    thick a pixel each side; double either side of a **white** gridline pixel (the Paper, not the
    `#e0e0e0` gridline); dashes 8 at 100% and 9 at 150%.
  - It also reads: rows all 28 px with medium and thick lines; cases 4, 5, 6, 10 and 12-1; case
    11's inside lines and its outline over the bottom and right lines, under the MudBlazor Chrome,
    whose outline is not as black as the lines; and the twelfth run's case 14.
- The rest of layer 3 is CI's.

### Case 11's top and left: needs a decision

In Excel the Selection's outline lies on the gridline and the pixel outside it (`-1..0 217346` on
B2's top), so it covers the outer lines on all four sides.

ADR-0008 (2026-09-29, UX-18) draws ExGrid's outline wholly inside the range, so that no layer above
the selection layer cuts its outer pixel. The outline covers the outer lines on the bottom and the
right, which lie on the range's own last pixels. On the top and the left the line lies in the cells
above and to the left: it stays drawn just outside the outline, a pixel or two from Excel.

`sheet-borders.spec.mjs` holds this as a `test.fixme`, named for the decision. Two ways out, each
the user's call:
- (a) move the outline 1 px outward on the top and the left, onto the gridline, as Excel does. It
  then goes under the Headings' edge and a pinned block's edge, the thinning UX-18 removed.
- (b) record the difference as ExSheet's, beside ADR-0057's for Reference Outlines.

### Readings no run observed

- **A gap in a dashed line shows the gridline** (`#e0e0e0`) behind it. Case 9's legend tells only
  dark from light.
- **A line on column A's left that reaches into A3** (thick or double) draws its inner pixel there.
  The twelfth run's case 14 saw a thin line only, which lies under the Headings' edge whole.

### Seen on the way

- **The Selection's shade lies over the lines inside it**, tinting their black, as DC-59 decides
  (lines below the Selection). Excel's shade (`#C7C7C7`) left them `#000000`. They read dark, which
  is what layer 3 asserts.
