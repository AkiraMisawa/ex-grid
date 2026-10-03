# render-bench — a disposable render-cost harness

Built to measure, before the specification was frozen, **how much the shape of the API costs at
render time**. Throw it away once the conclusions are in.

## What it measures

The hypothesis that cost is decided by **the number of cells touched per render**, not by the row
count. The data is already in memory, and **only rendering is measured** (fetching, sorting and
filtering are excluded — [ADR-0001](../../docs/adr/0001-consumer-pushes-the-window-grid-does-not-fetch.md)
puts them on the Consumer side, so the grid never walks every row).

Six modes stack up, isolating **the cost of one design decision each**:

| Mode | What it adds |
|---|---|
| `Direct` | the floor with no abstraction: plain markup, direct field access |
| `Accessor` | a per-column `Func<Row, object>` accessor (boxes every decimal) |
| `AccessorMeta` | a per-cell metadata lookup (a Cell State probe, keyed on (group, metric)) |
| `RowComponent` | the boundary at the row: row is a component, cells stay plain markup |
| `RowComponentTone` | `RowComponent` + a per-cell tone rule: a Consumer delegate on the value answering a closed enum, painted as an interned class ([ADR-0006](../../docs/adr/0006-grid-owns-a-generic-cell-state-vocabulary.md)) |
| `Component` | the boundary at the cell: every cell is a Blazor component |

There is a second benchmark for **painting the selection**: a CSS class per cell versus a single
absolutely positioned overlay, with scrolling held fixed so only the selection moves.

The bar is **16.6 ms** (one frame at 60fps). A median past that is what "sluggish" means.

## Per-cell appearance (`/format`, ticket 44)

A second page, <http://localhost:5199/format>, measures what a Cell Format costs per painted cell
([ADR-0071](../../docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md),
"How it is painted: measured first"). Every mode is built on `RowComponent`: the row is the
memoisation boundary, cells are plain markup, and a row's appearance travels in an immutable object
per row, whose reference is the row's change signal.

| Mode | How the appearance is painted |
|---|---|
| `RowComponent` | nothing: the same row with no Cell Format, the baseline |
| `CellFormatInline` | Font colour, Fill and bold as an inline `style` on each formatted cell, one interned string per format |
| `CellFormatVars` | per-cell custom properties (`--fc`, `--fb`, `--fw`) read by one static rule on every cell |
| `CellFormatClasses` | interned classes, one per distinct format, written into a generated `<style>` |
| `BorderLayer` | a layer over the rows: one element per run of the same line along a painted row (bottom edges) or down a painted column (right edges), positioned against the painted slice as the selection overlay is (ADR-0008). Rows know nothing of borders |
| `BorderInCell` | each cell draws its resolved bottom and right edges inside its own box, in place of its gridlines, through interned classes |
| `BorderLayerPerRow` | not one of the ticket's two: `BorderLayer` split into one keyed, memoised strip per painted row, with one segment per right edge. Measured because `BorderLayer` renders every run on every scroll step |
| `BorderInCellExcel` | not one of the ticket's two: each cell paints its own share of Excel's line on its four edges, at the eleventh Windows run's geometry (case 9), as background layers over its Fill. Nothing is painted outside the row, and no cell's box changes size |
| `BorderInCellExcelBox` | the same pixels with no custom property and no per-cell gradient: solid lines are the cell's own borders, the pixel past the gridline (thick, double) and double's inner row are inset shadows, and a dash pattern is a pseudo-element whose rule belongs to the line |
| `BorderInCellHybrid` | ticket 47: `BorderInCellExcel`'s pixels with every solid line on and above the gridline (thin, medium, thick's upper two) as the cell's own bottom or right border, the right padding narrowed by what a wider border takes; background layers only for the dashes, double and the pixel past the gridline. Interned per side, style and colour |
| `BorderInCellExcelFull` | ticket 47: `BorderInCellExcel` with each solid share a gradient over the whole cell whose hard stop falls on the device pixel, in place of a tile one or two device pixels high; the dash patterns stay tiles. What the product draws |
| `BorderInCellExcelFill`, `BorderInCellHybridFill` | ticket 47: the two above with a Fill on every bordered cell, from one interned class per colour, so the background colour and the line layers are painted on the same cells |

Each Font and Fill mode runs over a share of formatted cells (10%, 50%, 100%) and N distinct formats
(1, 16, 256); a formatted cell carries a colour, a background and bold. The border modes run over the
same grid; a formatted cell records the same line on its four sides (Excel's "All Borders"), and an
edge recorded on both sides is resolved to one line before painting, the upper (left) cell's. The
lines cycle through Excel's thirteen styles, so every border mode draws the same edges:

- **The layers** place a line on the gridline's pixel and back a 2- or 3-px line up by one, which is
  Excel's geometry for thin, medium, thick and double (`verification/2026-10-01-windows-excel-11`,
  case 9). The dash patterns are CSS's `dashed` and `dotted`, not Excel's.
- **`BorderInCell`** draws the whole line inside the upper (left) cell, ending on its gridline, with a
  CSS border. Medium is then Excel's; thick and double sit one pixel high; and a 2- or 3-px border
  narrows the cell's content box, which moves its text.
- **`BorderInCellExcelBox`** draws the same pixels from plain declarations, written out in CSS pixels
  per resolution (1 and 1.5): a solid line is the cell's bottom or right border, 1 or 2 px wide, with
  the right padding narrowed so that no text moves; the 1-px part of a thick or double line past the
  gridline, and double's dark row before it, are one inset `box-shadow`, whose combinations are
  classes added to the sheet as they appear; a dash pattern is an absolutely positioned `::after`
  (bottom) or `::before` (right) whose gradient rule is the line's own, and only such a cell is
  positioned. It is the variant built to find out where `BorderInCellExcel`'s cost comes from.
- **`BorderInCellExcel`** has the upper (left) cell paint the gridline's pixel and what lies above
  (left of) it, and the lower (right) cell the pixel below (right of) it, which only thick and double
  have. Each part is a background layer: a solid gradient, two rows for double (the gridline's pixel
  white), or a `repeating-linear-gradient` with Excel's dash lengths (8 on 3 off, and so on). Generated
  classes per side and line (`eb5`, `er5`, `et5`, `el5`) set custom properties, and one static rule
  reads them: four rules per line, never one per combination of four sides. A cell's top edge comes
  from the row above, so a border change can repaint the rows either side of it. The data is random, so at N 16
and above almost no two neighbours share a line: that is the layer's worst case, and N 1 at 100% is
its best (one run per gridline).

Each configuration measures five phases, and **each step records three numbers**:

- **render**: Blazor's render plus the DOM update, as the modes above are measured, stamped in
  `OnAfterRender`;
- **layout**: the style recalculation and layout that render caused, forced synchronously right after
  it (inline styles, custom properties and classes differ mostly here, which the render number cannot
  see);
- **paint**: the next frame's main-thread paint and commit, from its animation frame to the first task
  after it. Rasterisation and compositing run on other threads and are not in it.

*Frame* is their sum. The phases:

| Phase | What changes | What the rows must do |
|---|---|---|
| Fling | scroll by `Rows moved per step` (50) | every painted row is new, so all mount |
| Slow | scroll by one row | exactly the entering row mounts; the rest skip |
| No change | nothing | nothing renders |
| One cell | one cell's format | its row renders; a border also re-resolves the rows either side, and repaints those whose edges changed (at most 2 rows in `BorderInCell`, 3 in the two Excel modes, 0 rows in the layers) |
| Column change | a new format over a whole column (so a generated sheet grows by a rule) | every painted row renders, except in the layers, where no row does |

The *Rows skip* column says whether every expectation held. The model's own work (copying the
format arrays, resolving edges) happens before the clock starts. The modes interleave within each
share and N, so that whatever else the machine is doing lands on every mode alike, and the baseline
runs first and last, so a drift across the run shows. Differences of a millisecond are only worth
reading from a quiet machine: note the load average beside the result.

Run it by hand with **Measure cell format**, or headless:

```sh
nix develop -c dotnet run -c Release --project Bench.Host --urls http://0.0.0.0:5199
"$CHROME" --headless=new --remote-debugging-port=9222 --user-data-dir=/tmp/p about:blank
nix develop .#browser -c node tools/cdp-run.mjs http://127.0.0.1:5199/format 200 3600 50 20 "Measure cell format" save
```

**Measure the hybrid and a Fill with borders (ticket 47)** runs `BorderInCellExcel`,
`BorderInCellHybrid`, `BorderInCellExcelFull` and the two Fill variants beside a `RowComponent` baseline
of their own at six shares and Ns (pass `"Measure the hybrid and a Fill with borders"` as the driver's
button).

`CDP_PORT` points the driver at another debugging port. **Measure cell format (quick: 50%, N 16)**
runs one configuration per mode. **Measure borders** runs the border modes only, each share and N
beside a `RowComponent` baseline of its own, to be read as deltas against it (pass `"Measure borders"`
as the driver's button). **Show** puts one configuration on screen, to look at or to scroll by hand
with the frame measurement, and **Show the thirteen line styles** draws one black box of each style
in the chosen border mode, to compare with Excel's pixels.

**The Excel modes at other scales.** Their lengths are device pixels, as Excel's are (`--dp`, set per
resolution in `app.css`; the Box variant writes its lengths out per resolution). At device scale 1
both match the eleventh run's case 9 to the pixel, the dash phase aside (slanted dash-dot is one
pattern on both rows). At 1.5, under headless Chrome's device-scale emulation, neither does:

- `BorderInCellExcel`: the 1-px parts and the dash patterns are crisp and in place, but a 2-px part
  (medium, the upper part of thick and double) comes out about 1.5 device pixels with an antialiased
  row. A background tile of a fractional CSS size does not land on device pixels.
- `BorderInCellExcelBox`: Chrome computes a 0.667px or 1.333px border as `1px`, so every line drawn as
  a border is 1.5 device pixels.

*(Ticket 47, 2026-10-01.)* That was CDP's emulation, which lays the page out in CSS pixels and
scales the result, and the bench's scroller has a 1px border, which puts every cell edge on a half
device pixel at 1.5. Chrome given the scale on its command line (`--force-device-scale-factor=1.5`,
as an OS display scale gives it, and as browser zoom lays out) lays out in device pixels: there
`BorderInCellExcel`, `BorderInCellExcelFull` and `BorderInCellHybrid` all draw case 9's pixels at 1.5,
and the same at 1. Under emulation, on cells whose edges fall on device pixels, a whole-cell gradient
and an inset shadow stay exact, while a tile of a fractional CSS height and a border are blended
over two rows. Windows has not been checked.

## Running it

```sh
nix develop -c dotnet run -c Release --project Bench.Host --urls http://0.0.0.0:5199
```

Open <http://localhost:5199>. Under WSL2, if `localhost` does not reach it from the Windows
browser, use the address from `hostname -I`.

1. Pick a preset (`Fling (40×20, 50 rows)` is the harshest realistic case)
2. **Measure all modes**
3. **Save results to the server** → lands in `spikes/render-bench/results/{timestamp}.json`

For the felt experience of manual scrolling: `Start frame measurement` → drag the grid → `Stop and
summarise`.

A headless run is possible with `nix develop .#browser`, driven over the Chrome DevTools Protocol.

## Caveats on the measurement conditions

- **Measure in Release.** Debug is a different kind of slow.
- The programmatic measurement covers **Blazor's render plus the DOM update**. `StateHasChanged()`
  can complete a render synchronously, so the browser's layout and paint are left outside the
  timed region, with 1 ms yielded after each iteration. **End-to-end frame time is what the manual
  scrolling measurement is for.**
- Headless Chromium works but renders in software, so **its absolute numbers diverge from real
  hardware**. Use headless to reproduce exceptions; take numbers on a real browser. (The same
  measurement gave a 11.7 ms maximum headless and 19.7 ms on real hardware.)
- **No AOT** (it needs the `wasm-tools` workload). These are Release IL numbers; AOT may leave more
  headroom.
- 5,000 rows are generated and cycled with a modulus. To confirm that render cost does not depend
  on the row count, change `TotalRows` and re-run.

## This code is deliberately a bad example

Two things here **must not be carried into product code**
([ADR-0018](../../docs/adr/0018-multiple-instances-must-be-independent.md)):

- CSS class names `.r` `.c` `.sel` `.window` `.scroller` — no prefix; they would collide with a
  host application
- `window.bench` — a single global; a second instance would break the first

They are acceptable only because this is disposable.
