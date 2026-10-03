# 90: A pinned cell with lines keeps its row gridline

Status: done

**What to build:** a defect ticket 48 found. On the Sheet, a Pinned Column's cell paints its row's gridline
itself, as a background image, because its own ground covers the row's. A cell that draws a line layer
(`.ex-lined`, ADR-0050 item 15) replaces that background image, so the gridline goes. Ticket 81 kept a tint
under the lines with `--ex-tint`. The gridline needs the same.

**Blocked by:** None (can start immediately)

- [x] **A pinned cell with lines on any side keeps its row gridline**, under the lines, where Excel
      would show the gridline.
- [x] **Ticket 81's tint and ticket 85's single tint** still hold.
- [x] **Tests:** layer 2 for the stylesheet; layer-3 pixels in a spec. CI runs them.

## Comments

2026-10-01, agent cf-88.

### What was built

The gridline is named in a custom property and painted as a layer, as ticket 81 did for the tint.
- **The core** (`src/ExGrid/wwwroot/ex-grid.css`). `.ex-cell.ex-lined` gains one last layer,
  `var(--ex-row-rule, none)`: the row's rule, where a cell whose own ground hides the row's names
  it. It lies beneath every other layer: the lines, the tint, the covers (a neighbour's Fill over a
  gridline this cell holds) and the column rule. So a line on the gridline still covers it, and a
  Fill still covers it, as Excel's does. The core names no `--ex-row-rule` itself, so a bare
  ExGrid's lined cells paint as before.
- **ExSheet** (`src/ExSheet/wwwroot/ex-sheet.css`). A Pinned Column's cell without a Fill names its
  gridline in `--ex-row-rule`, and paints `background-image: var(--ex-row-rule)`. Where the core's
  `.ex-lined` replaces that image, it now paints the same gridline at the bottom of its layers.

The case that shows it today needs no Borders. ExSheet does not declare Borders until ticket 49,
but a cell already draws the line layer when a neighbour's Fill covers a gridline it holds. On
`/sheet?case=4`, B2 is yellow, so the pinned A2 holds a cover over its right gridline. Before this,
A2's bottom gridline was the Paper. Lines on any side reach the same `.ex-lined` rule, so they keep
it the same way once ticket 49 declares them.

### Tickets 81 and 85

- The tint is still the fifth layer, under the lines and over the covers. The new layer is added at
  the bottom, and is `none` wherever no rule names it.
- `CellAppearanceTests` (ticket 81's and 85's layer-2 tests) pass unchanged, with the size and
  position lists one longer.
- In layer 3, `appearance.spec.mjs --grep tint` passed 2 of 2, headless on this Mac. Those are the
  lined group, total and Missing cells.

### Tests

- **Layer 2.**
  - `CellAppearanceTests.The_lined_rule_paints_a_named_row_rule_beneath_every_layer`: the last layer
    is `var(--ex-row-rule, none)`, at 100% 100% and 0 0, and no core rule names one.
  - `PaperStylesheetTests.A_pinned_cells_gridline_is_named_for_the_line_layer`: `ex-sheet.css` names
    the gridline in `--ex-row-rule` on a Pinned Column's cell without a Fill, and paints it from
    there.
  - Both fail on the tree before the fix: the last layer was the column rule, and nothing named
    `--ex-row-rule`.
- **Layer 3.** `sheet-paper.spec.mjs` › "a pinned cell that draws a line layer keeps its row gridline
  beneath it", under both Chromes. On `/sheet?case=4` it reads:
  - A2 is pinned and lined, and A1 is not;
  - A1's and A2's bottom pixel rows are `#e0e0e0`;
  - A2's right pixel column is still B2's yellow;
  - A2's inside is still the Paper.

  Headless on this Mac, WebAssembly, `--project=chrome`: it failed with only the core's half in place,
  because A2's gridline read `255,255,255`. It passed 2 of 2 with the fix.

### Seen on the way, not built

The core's own Pinned Column cells hide the row rule under any theme that turns the rule on, as
ExGrid.MudBlazor does. Their ground is opaque, and the row's rule is painted on the row beneath
them; no core rule paints it on a pinned cell. This was read from the stylesheets, not observed in a
browser. ExSheet worked around it for the Sheet in ticket 48. The core could name `--ex-row-rule` on
`.ex-pinned` itself, which would change ExGrid's look under MudBlazor, so it is left for a decision.

2026-10-01, agent cf-48: ticket 49's case 11 made `/sheet?case=…` pin nothing, as the run's workbook had no
frozen panes, so case 4's A2 was no longer a pinned cell.
- `/sheet` takes `?pin=N` now, and the test opens `?case=4&pin=1`.
- `sheet-paper.spec.mjs` passed 17 of 17 on both hosts in `chrome`.
