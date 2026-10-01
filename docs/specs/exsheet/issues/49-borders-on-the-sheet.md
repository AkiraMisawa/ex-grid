# 49: Borders on the Sheet, as Excel draws them

Status: done

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
- [x] Inside the Selection, borders stay drawn over its shade; the Selection's outline covers the
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

*(2026-10-01, orchestrator.)* **Case 11 decided with the user: (c).** The outline lies on the gridline and one
pixel outside the range, as Excel's does. Beside the Headings, a Pinned Column and the header it stays inside, so
UX-18 holds. ADR-0008's note of this date records it.
- [x] Build it, and turn the case-11 `fixme` into a test of all four sides in the open.
- [x] Add a test that the outline's sides keep equal widths beside the Headings, a Pinned Column and the header
      (UX-18).

DC-48 is ticket 86's.

2026-10-01, agent cf-48: case 11, as decided.

### What was built

- **The Selection's outline lies where Excel's does.** That is the Focus's outline, and the one
  round a Selection of a single range.
  - It sits on the gridline and one pixel outside the range: the outline's width above the top and
    left of the left; on the bottom and the right, the range's own last pixel (the gridline) and one
    past it.
  - So it covers a Border on every outer edge.
- **Where a side stays inside.** A side whose outer pixels something above the selection layer would
  cover stays inside the range, as before, so its width stays equal (UX-18):
  - a top edge at or above the readable area's top, under the header;
  - a left edge at or left of the Row Headings' edge;
  - for a scrollable column, a left edge at or left of the pinned block's edge, scrolled sideways
    too.
- **How the core decides it.** It works this out from geometry it already holds
  (`SelectionStyles.OutlineSides`, from `ExGrid.OutlineCoverAt`): the first row whose top lies below
  the header, and the horizontal scroll offset. It marks a side that stays inside with
  `--ex-outline-in-t` or `--ex-outline-in-l` on the element's inline style.
- **How the stylesheet draws it.** The outline is the border of a box of its own, the element's
  `::after`, so a side moves without moving the range's tint.
  - `ex-grid.css` places that box from the two markers, the Focus outline's 2px and
    `--ex-rule-width`. Nothing in C# reads the outline's width.
  - A range across the pinned boundary is clipped to its side on that edge only. Its clip lets a
    row's height past its other edges, more than any outline is wide.
  - Under forced colors every range and the Focus are outlined inside their boxes as before, with
    no `::after`.
- **The DemoHost's case pages no longer pin column A** (`/sheet?case=…`). The run's workbook had no
  frozen panes, so Part C now compares like with like. The page's own Sheet still pins A.

### Tests

- **Layer 2** (`SelectionLookTests`):
  - which sides stay inside, for the Focus at the corner, the top and the left edges and in the
    open;
  - a range's top under the header when scrolled, and the row below in the open;
  - a scrollable column's left beside the pinned block, and the same scrolled sideways;
  - a column's left beside the Row Headings.
  - The style strings the earlier tests pin now carry the markers and the new clip. A Reference
    Outline's comparison with a range's or the Focus's box leaves the markers out.
- **Layer 3**, `--project=chrome`, WebAssembly, headless on this Mac:
  - `sheet-borders.spec.mjs`, 34 passed, and 29 passed in `chrome-150`. Case 11's `fixme` is now a
    test under MudBlazor's Chrome: on all four outer edges of B2:C3 the gridline's pixel and the
    one outside it are the outline's, the range's own pixel past the outline is not, and the lines
    inside stay dark over the shade.
  - `selection-look.spec.mjs`, 15 passed. That includes the new UX-18 test: a single range's
    outline has four equal edges beside a Pinned Column, beside the Headings, under the header and
    in the open. The UX-19 tests now read the outline from the `::after`.
  - `sheet-paper.spec.mjs`, 9 passed.
  - `mud.spec.mjs`'s UX-9 test, 1 passed. It reads the outline a pixel above the cell now.
  - The rest is CI's.
- **Layers 1 and 2**: ExGrid.Tests 874, ExSheet.Engine.Tests 2317, ExGrid.MudBlazor.Tests 133,
  ExGrid.Components 1279 (one skipped), ExSheet.MudBlazor.Tests 38, ExSheet.Components.Tests 583.

Part C (SH-46) is the Windows session's run by hand. The DemoHost shows its cases.

2026-10-01, agent cf-48: **the lines stay on the device pixels wherever the page puts the Sheet.**
CI (Linux, headed, `chrome-150`, the Server host) read a dotted line on D7's right as 2, 1, 3, 1 and a
medium dash-dot-dot on D13's right as off pattern, where Excel's are 2, 2, 2, 2 and so on.
- **The cause.** The text above the Sheet, in CI's fonts, put the grid at a fraction of a device
  pixel. The rows' layer (`.ex-viewport`) was composited on its own (`will-change: transform`, there
  since the first virtualisation, with no measurement behind it), so it was rasterised at that
  fraction, and every line drawn in device pixels blended across two.
- **Reproduced here.** The Sheet was moved a third of a CSS pixel across and down: every right-edge
  line failed at 150%.
- **The fix.** The layer is no longer composited. Painted with its parent, it is snapped to the
  device pixels as any box is.
  - Its transform still makes it the stacking context the selection layers rely on (ADR-0008).
  - The rounding of its offset to a device pixel stays, because it is still needed: without it, 17
    of 26 lines failed at 150% scrolled.
- **Scrolling measured the same.** Frame intervals were within 0.2 ms at the median with and without
  the layer, on `/wide` (slow and flung) and on `/sheet` at 100% and 150%, headless.
- **The new test.** `sheet-borders.spec.mjs` reads the lines with the Sheet moved by a third of a
  pixel across and down, at 100% and in `chrome-150`. Thin, thick, double, dotted and medium
  dash-dot-dot, on a bottom and on a right edge.
- **Results.** `sheet-borders.spec.mjs` passed 30 of 30 on the Server host in `chrome-150` and 35 of
  35 on WebAssembly in `chrome`; `selection-look.spec.mjs` passed 15 of 15. Layers 1 and 2:
  ExGrid.Tests 1001, ExSheet.Engine.Tests 2333, ExGrid.MudBlazor.Tests 168, ExGrid.Components 1293
  (one skipped), ExSheet.MudBlazor.Tests 43, ExSheet.Components.Tests 592.
- ADR-0053's note of this date says the viewport is composited; that sentence is now out of date.

2026-10-01, agent cf-48: two tests knocked on by case 11, both fixed.
- **Ticket 47's test of the Focus over a line** read the outline inside the cell. It now reads it on the
  gridline and the pixel past it (ticket 47's comment).
- **Ticket 90's pinned-cell test** read case 4's A2 as a pinned cell. Case pages stopped pinning column A
  with case 11. `/sheet` gains `?pin=N`, and that test opens `?case=4&pin=1`. Part C's pages still pin
  nothing.

2026-10-01, agent cf-48: CI's line tests failing at 150% on the Server host (run 36929390859) were the
tests reading too early. The page was not putting the lines off a device pixel.
- **What failed.** It was the per-style test, not the shifted-Sheet one. Eleven styles and sides read no
  line at all, or a neighbouring row's style. Locally, the spec passed on the Server host at 150% with
  the Sheet moved by every fifteenth of a pixel, down and across. Behind an 80 ms round trip, 22 of 26
  line tests failed with CI's messages.
- **Why.** At 150% the Sheet is compressed (k = 1.31), so its rows move when the grid is told of the
  scroll: a round trip later on the Server host. `scrollRowToTop` returned before that. The box was read
  from the old slice and the picture taken of the new one. On WebAssembly the grid is told in-process.
- **The fix.** `scrollRowToTop` (fixtures.mjs) now waits until the grid has painted for the offset the
  browser holds. The offset the grid writes on the Viewport, before rounding, plus the row's place in
  the Viewport, must equal ADR-0053's r × h − c(s) + s. The row's position alone is no witness: scrolled
  to row 0 it moves 0.42 px, within the third of a pixel the slice is rounded by. Behind 80 ms and
  300 ms round trips, 30 of 30 pass. No line or pattern was loosened.
- **The shifted-Sheet test never moved the Sheet down.** A margin of a third of a pixel collapsed into
  its neighbour's. It now uses padding, which moves the cells (measured).
- **Results.** `sheet-borders.spec.mjs`: 30 of 30 at 150% and 35 of 35 at 100%, on both hosts.
  `scrollRowToTop`'s other callers pass on both hosts: MK-6, the Sheet's items 3 and 5, active-cell
  cases 7 and 8, and UX-15. Layers 1 and 2: ExGrid.Tests 1011, ExSheet.Engine.Tests 2334,
  ExGrid.MudBlazor.Tests 168, ExGrid.Components 1322 (one skipped), ExSheet.MudBlazor.Tests 44,
  ExSheet.Components.Tests 594. ExGrid.Components failed once on MEM-1's allocation measurement
  (25.7 MB against 27.2 MB) with every suite running at once. It passed twice alone.
