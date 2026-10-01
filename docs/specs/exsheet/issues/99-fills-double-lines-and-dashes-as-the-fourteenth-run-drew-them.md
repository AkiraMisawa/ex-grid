# 99: Fills, double lines and dashes as the fourteenth run drew them

Status: done

**What to build:** ticket 47's painting where the fourteenth Windows run (cases 16 to 18) answered
otherwise than ADR-0071 read. ADR-0071, "What the fourteenth Windows run settled", ticket 47's painting:

- **Where two filled cells meet, the gridline between them takes the lower cell's Fill.** Between two
  filled cells side by side, the right cell's. Where only one of the two is filled, its Fill covers the
  gridline as today.
- **A double line's middle pixel shows what the gridline beneath it would show:** the Fill that covers
  that gridline (by the rule above), or the ground where neither cell is filled.
- **The long dash is 9 device pixels at every scale.** Today it is 8 below 150% (`--ex-dash`). Thin
  dashed stays 3 on and 1 off, in device pixels.

**Blocked by:** None (can start immediately)

- [x] **Two filled cells, one above the other:** the gridline's device pixel is the lower cell's Fill.
      Side by side: the right cell's. One filled cell still covers all four of its gridlines.
- [x] **A double bottom over a filled cell:** dark, the Fill, dark. With no Fill on either cell: dark,
      the ground, dark, as before.
- [x] **Medium dashed reads 9 on and 3 off** in device pixels at 100% and at a real 150%. Every style
      that uses the long dash takes it.
- [x] **No row height changes, and nothing per cell reaches JavaScript** (ADR-0027 P1 to P9).
- [x] **Layer 1/2 for the generated rules. Layer-3 pixels at 100% and 150%** (SH-46, DC-59). CI runs
      layer 3.

## Comments

2026-10-02, agent cf-99.

### What was built

- **Which Fill covers a gridline** (`src/ExGrid/Components/CellAppearances.cs`, `Resolve`). A cell
  holds the gridline of its bottom and right edges. A Fill covers it beneath any line there: the
  lower (right) cell's where that cell has one and it differs from this cell's own, else this cell's
  own, which its ground already paints. Before, a cover was given only to an unfilled cell, and only
  where no line lay on the edge. So two filled cells one above the other now show the lower one's
  Fill on the gridline between them (case 16), and side by side the right one's (ADR-0071's reading).
  A filled cell beside unfilled ones covers its four gridlines as before.
- **The cover is no longer a share instead of a line** (`AppearanceStyles.ClassFor`). It is passed
  beside the four shares, and named as before (`ex-lb-cover-…`, `ex-lr-cover-…`). Its rule also names
  its colour, `--ex-cover-b-color` or `--ex-cover-r-color`. A Fill's rule names its own,
  `--ex-fill-color`. The stylesheet still grows only per side, style and colour, never per
  combination.
- **A double line's middle pixel** reads
  `var(--ex-cover-b-color, var(--ex-fill-color, var(--ex-background, Canvas)))`, or the same with `r`.
  That is the lower (right) cell's Fill, else this cell's, else the ground (case 17; with no Fill, the
  eleventh run's case 9 as before). It is named rather than left transparent, because beneath a
  gridline no Fill covers lies the grid's rule, which Excel does not show there. One rule per double
  line's side and colour, whatever Fills lie beneath it.
- **The long dash is 9 device pixels at every scale** (case 18). `--ex-dash` is gone from
  `ex-grid.css`. `AppearanceStyles.Dash` is the constant 9, and every stop of a dash tile is one
  `calc(n * var(--ex-dp))`. It applies to every style with a long dash: dash-dot, dash-dot-dot, and
  medium dashed, medium dash-dot and medium dash-dot-dot. Thin dashed stays 3 on and 1 off.
- **A consequence of "beneath any line"**: the gaps of a dashed line on a gridline that a lower (right)
  Fill covers now show that Fill, where before they showed the row's rule. No run looked at this. It
  follows ADR-0071's "A Fill covers all four of its gridlines, beneath any line".
- **DemoHost cases** (`samples/ExGrid.DemoPages/SheetCases.cs`):
  - `14-16`, `14-17` and `14-18` are the fourteenth run's set-ups.
  - `fills` holds the arrangements no run looked at: B2/C2 side by side; a double on E2's bottom over
    E3's Fill; on B5's right between two Fills; on E5's right beside F5's Fill; and on B8's bottom
    between two Fills.

### What was verified, and how

- **Layers 1 and 2**, `nix develop -c dotnet test ExGrid.slnx`: 1089 + 2334 + 198 + 1362 (1
  skipped, as before) + 44 + 597, all passing.
  - New: `CellAppearanceTests.Between_two_fills_the_gridline_takes_the_lower_or_right_cells`,
    `A_fill_covers_its_gridline_beneath_a_line`, `A_double_lines_middle_shows_what_its_gridline_would`
    and `Every_long_dash_is_nine_device_pixels` (four styles).
  - New: `SheetAppearanceTests.Between_two_fills_the_gridline_takes_the_lower_or_right_cells`, the run's
    case 16 through ExSheet.
  - Changed to the new readings: the Fill rule's text (`CellAppearanceTests`, `PinnedRowRuleTests`),
    the double's middle pixel (was always the ground), and the dash tile (was `--ex-dash`).
- **Layer 3**, WebAssembly host on port 5399, headless Chrome, `sheet-borders.spec.mjs` and
  `appearance.spec.mjs` under `chrome` and `chrome-150`: 136 passed. The full suite is CI's.
  - New in `sheet-borders.spec.mjs`, each named DC-59 so it runs at a real 150% too:
    - case 16: the gridline between B2 and B3 is B3's light blue, the device pixel above it yellow, and
      every other gridline round each cell its own Fill's;
    - side by side: the right cell's;
    - case 17: dark, yellow, dark;
    - `fills`: the four double-line arrangements, each dark, the expected Fill, dark;
    - case 18: B2 reads 9, 3, 9, 3, 9 along both of its rows, and B4 3, 1, 3, 1, ….
  - At 150% a covered gridline is exactly one device pixel. The pixel before it is still the upper
    Fill, not a blend, so that pixel is asserted at both scales.
  - Changed: `expected()` in `line-pixels.mjs` and `appearance.spec.mjs` reads the long dash as 9 at
    every scale (was 8 below 1.5), citing case 18.
- **A test floor that had to move.** `appearance.spec.mjs` required at least three whole runs along
  every dashed line.
  - On `/appearance`, rows are Excel density, 20 px. A right edge is one row, and it holds only medium
    dashed's 9 on and 3 off before the next 9 is cut (9 + 3 + 9 = 21). The old floor held only
    because the dash was 8.
  - The floor is now "three, or every whole run the edge holds where it holds fewer", computed from
    the expected pattern and the length read. It is still three for every other style and scale, and
    still fails a line that is not drawn.
  - The runs read are still compared exactly, so medium dashed's right edge at 100% is checked on 9,
    3.
- **P1 to P9.** No row height changes: `DC-59: rows keep their one height` passed, and nothing here
  sets a length C# computes with. No JavaScript was added or changed. A filled cell under or beside
  a different Fill now carries `ex-lined` and one cover class. That is one more interned class, and
  nothing per cell reaches JavaScript.
- **Not measured**: the cost of that extra layer on a sheet whose rows alternate Fills. Every cell of
  such a sheet is now `.ex-lined`. Ticket 47 measured a Fill and a line on one cell at +0.1 to
  +2.6 ms. Performance never gates; a `render-bench` mode would answer it if it matters.
