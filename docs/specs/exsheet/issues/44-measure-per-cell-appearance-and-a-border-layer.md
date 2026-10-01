# 44: Measure per-cell appearance and a border layer

Status: ready-for-agent

**What to build:** [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "How it is painted: measured first", which keeps ADR-0046's precondition.
Add modes to `spikes/render-bench` that measure what a Fill, a Font and Borders cost per painted
cell, and bring the numbers back. **The choice is the user's.** The agent reports the results with
a recommendation. The orchestrator puts them to the user, and records the decision as an addendum
to ADR-0063. The agent does not edit the ADR.

**Blocked by:** None (can start immediately)

- [x] **Three Font and Fill modes.** In each, a share of the painted cells (10%, 50% and 100%) carries
      a colour, a background and bold, drawn from N distinct formats (1, 16 and 256):
  - `CellFormatInline`: an inline `style` on each cell.
  - `CellFormatVars`: per-cell custom properties, read by one static rule.
  - `CellFormatClasses`: interned classes, one per distinct format, written into a generated
    `<style>`.
- [x] **Two Border modes**, over the same shares:
  - `BorderLayer`: a layer over the rows, one element per run of the same line along a painted row
    or column, as the selection overlay draws ranges (ADR-0008).
  - `BorderInCell`: lines inside each cell's box.
- [x] **The measurements**:
  - scroll frame cost (median, p95 and max), measured as the existing modes are;
  - renders per scroll step;
  - the time for one format change over a whole column.
- [x] Rows still skip their render when nothing of theirs changed, in every mode (P1–P9, ADR-0027).
- [x] The result JSON is kept beside the existing results, and `README.md` lists the modes.
- [x] **The report**: the numbers, and a recommendation for Font and Fill and one for Border. If
      the layer is too costly, say so: the fall-back (lines inside each cell) is the user's call
      (ADR-0063).

## Comments

2026-10-01, the measurement (agent cf-44). **The choice is the user's; below are the numbers and a
recommendation.** All runs are headless Chrome 154 on an Apple M4 Pro Mac, so the absolute numbers are
software rendering's, not real hardware's. Read the deltas, not the totals. **A run on real hardware,
by hand, is still owed**, and so is one on Windows.

### What was built

A second page in `spikes/render-bench`, `/format`, with the ticket's five modes and three more.
`README.md` lists them, with how to run each sweep.

- **The ticket's modes:** `CellFormatInline`, `CellFormatVars`, `CellFormatClasses`, `BorderLayer`
  and `BorderInCell`.
- **Three more, measured beside them:**
  - `BorderLayerPerRow`: the layer split into one memoised strip per painted row.
  - `BorderInCellExcel`: each cell paints its own share of Excel's line, at the eleventh run's case 9
    geometry, as background layers.
  - `BorderInCellExcelBox`: the same pixels from borders, inset shadows and pseudo-elements, with no
    custom properties.
- **The base.** Every mode is built on `RowComponent`, with the appearance carried by an immutable
  object per row.
- **What each step records:**
  - **render:** Blazor's render and DOM update, measured as the other page measures it;
  - **layout:** the style recalculation and layout that render caused, forced right after it;
  - **paint:** the next frame's main-thread paint;
  - **frame:** their sum.
- **The phases:**
  - **fling:** 40×20 cells, 50 rows per step, 200 steps;
  - **slow scroll:** 1 row per step;
  - **no change;**
  - **one cell's format;**
  - **a new format over a whole column:** 40 steps.

### The result files, and the load each ran under

The load is the 1-minute load average, sampled every 30 s during the run. The idle machine sits at
about 2 to 3: the window server, a virtual machine and the Claude app.

| File | What | Load | Counts? |
|---|---|---|---|
| `results/20261001-010843-314.json` | first full run, the modes in blocks | not recorded | no: superseded by A |
| `results/20261001-113322-829.json` | **A**: full run, the modes interleaved | 2.0 to 3.2 | yes: the quiet run, and the reference for Font and Fill |
| `results/20261001-133041-335.json` | **B**: full run, interleaved | 3.8 to 6.3 (another session's layer 3 ran during it) | yes, as a repeat. Its baseline is 0.6 ms higher, and its ranking is A's |
| `results/20261001-153002-709.json` | **C**: the border modes, each share and N beside a baseline of its own | 3.0 to 4.5 | yes, for Borders |
| `results/20261001-160808-773.json` | **D**: the three in-cell modes, each share and N beside a baseline of its own | 3.5 to 5.9 | yes, for the in-cell variants |
| `results/20261001-geometry/` | the thirteen styles' pixel profiles, and two enlarged screenshots | – | the geometry check |

In runs A and B, the border modes drew seven CSS shapes, and the lower or right cell's side won an
edge. From C on, every border mode draws Excel's thirteen styles, and the upper or left cell's side
wins. So **C and D are the border numbers**, and A and B count for Font and Fill.

### Rows skip their render: yes, in every configuration of A, B, C and D

- A slow-scroll step mounts exactly the entering row.
- A render with nothing changed renders nothing.
- One cell's Font or Fill renders its own row and no other.
- A border change re-resolves the rows either side, and repaints only those whose edges changed:
  - up to 2 rows in `BorderInCell`;
  - up to 3 in the Excel modes, since the row below paints the part of a thick or double line that
    reaches into it;
  - no row in the layers. `BorderLayer` renders once; `BorderLayerPerRow` renders up to 2 strips.
- A column change renders the 40 painted rows, or none in the layers. `BorderLayer` renders once;
  `BorderLayerPerRow` renders 40 strips.
- Nothing per cell reaches JavaScript. The DOM grows only in the layers, by at most about 2 elements
  per painted cell (P1).

### Font and Fill (run A; B's delta beside it)

All deltas are against the run's own `RowComponent`, in ms. A fling frame is 40 new rows.

| Mode | Share | N | Fling frame p50 / p95 / max (A) | Δ p50 (A) | Δ p50 (B) | Fling render p50 (A) | Slow frame Δ p50 (A) | Column change frame p50 (A) |
|---|---|---|---|---|---|---|---|---|
| RowComponent | – | – | 13.1 / 13.7 / 15.0 | – | – | 8.1 | – | 8.8 |
| CellFormatInline | 10% | 1 | 13.8 / 14.5 / 15.8 | +0.8 | +0.4 | 8.6 | -0.2 | 8.8 |
| CellFormatInline | 10% | 16 | 14.1 / 14.6 / 15.4 | +1.1 | +0.8 | 8.7 | -0.1 | 8.9 |
| CellFormatInline | 10% | 256 | 14.3 / 14.8 / 15.7 | +1.3 | +1.3 | 8.8 | +0.0 | 9.3 |
| CellFormatInline | 50% | 1 | 14.7 / 15.4 / 15.7 | +1.7 | +1.3 | 9.1 | +0.1 | 9.4 |
| CellFormatInline | 50% | 16 | 14.8 / 15.5 / 16.6 | +1.8 | +1.3 | 9.1 | +0.2 | 9.5 |
| CellFormatInline | 50% | 256 | 15.0 / 15.6 / 16.3 | +2.0 | +2.1 | 9.1 | +0.5 | 9.5 |
| CellFormatInline | 100% | 1 | 14.6 / 15.3 / 16.5 | +1.6 | +2.1 | 9.2 | +0.4 | 8.8 |
| CellFormatInline | 100% | 16 | 15.4 / 16.8 / 26.9 | +2.4 | +1.6 | 9.4 | +0.7 | 9.1 |
| CellFormatInline | 100% | 256 | 15.1 / 16.7 / 19.8 | +2.1 | +1.8 | 9.2 | +0.5 | 8.9 |
| CellFormatVars | 10% | 1 | 13.7 / 14.2 / 15.8 | +0.7 | +0.6 | 8.5 | -0.0 | 9.2 |
| CellFormatVars | 10% | 16 | 14.0 / 14.8 / 15.4 | +1.0 | +0.8 | 8.7 | -0.0 | 8.9 |
| CellFormatVars | 10% | 256 | 14.3 / 14.9 / 16.9 | +1.3 | +1.1 | 8.8 | +0.0 | 9.2 |
| CellFormatVars | 50% | 1 | 14.8 / 15.3 / 16.4 | +1.8 | +1.3 | 9.1 | +0.1 | 9.3 |
| CellFormatVars | 50% | 16 | 14.8 / 15.5 / 17.9 | +1.8 | +1.5 | 9.1 | +0.2 | 9.3 |
| CellFormatVars | 50% | 256 | 15.1 / 15.8 / 17.4 | +2.1 | +2.0 | 9.2 | +0.6 | 9.5 |
| CellFormatVars | 100% | 1 | 14.6 / 15.2 / 16.6 | +1.6 | +1.6 | 9.2 | +0.2 | 9.2 |
| CellFormatVars | 100% | 16 | 14.9 / 16.0 / 23.5 | +1.9 | +1.6 | 9.2 | +0.8 | 9.1 |
| CellFormatVars | 100% | 256 | 14.8 / 15.4 / 16.6 | +1.8 | +1.8 | 9.1 | +0.5 | 8.9 |
| CellFormatClasses | 10% | 1 | 13.6 / 14.4 / 16.5 | +0.6 | +0.1 | 8.3 | -0.1 | 9.1 |
| CellFormatClasses | 10% | 16 | 13.8 / 14.4 / 15.3 | +0.8 | +0.7 | 8.4 | -0.2 | 9.1 |
| CellFormatClasses | 10% | 256 | 13.9 / 14.4 / 15.4 | +0.9 | +0.7 | 8.5 | -0.2 | 9.8 |
| CellFormatClasses | 50% | 1 | 14.1 / 14.8 / 17.5 | +1.1 | +0.7 | 8.5 | +0.1 | 9.4 |
| CellFormatClasses | 50% | 16 | 14.1 / 14.6 / 15.1 | +1.1 | +0.8 | 8.3 | +0.3 | 9.7 |
| CellFormatClasses | 50% | 256 | 14.5 / 15.5 / 17.3 | +1.5 | +1.1 | 8.5 | +0.5 | 9.8 |
| CellFormatClasses | 100% | 1 | 13.7 / 14.4 / 16.3 | +0.7 | +0.7 | 8.3 | +0.4 | 9.0 |
| CellFormatClasses | 100% | 16 | 14.0 / 14.6 / 15.9 | +1.0 | +0.7 | 8.4 | +0.8 | 9.3 |
| CellFormatClasses | 100% | 256 | 13.9 / 14.4 / 15.9 | +0.9 | +1.0 | 8.3 | +0.5 | 9.1 |

**Recommendation: `CellFormatClasses`.**

- It is the cheapest in every share and N of both runs: +0.6 to +1.5 ms at the median in A, and +0.1
  to +1.1 in B.
- Its tail is the tightest: p95 at most 15.5 and max at most 17.5. Inline and Vars reach maxima of 26.9
  and 23.5.
- Inline and Vars cost +0.7 to +2.4 ms. Both pay in render (a longer attribute per cell) and in
  style recalculation.
- A column change that adds a format, and so rewrites the generated sheet, costs Classes within
  0.5 ms of the others: 9.0 to 9.8 ms, against 8.8 to 9.5.
- One rule per distinct format keeps the sheet small. The number of formats is what grows it, not the
  number of cells.

### Borders (sweep C, each against the baseline of its own share and N)

| Mode | Share | N | Fling frame p50 / p95 / max | Δ p50 | Δ p95 | Slow frame Δ p50 | Column change frame p50 (base) | Column change renders: rows · layer | One cell: rows · layer (max) | Layer elements |
|---|---|---|---|---|---|---|---|---|---|---|
| BorderInCell | 10% | 1 | 13.8 / 14.3 / 15.4 | +0.4 | +0.4 | +0.3 | 10.0 (9.1) | 40 · 0 | 2 · 0 | 0 |
| BorderInCell | 10% | 16 | 13.9 / 14.3 / 14.4 | +0.3 | +0.4 | -0.1 | 10.1 (9.3) | 40 · 0 | 2 · 0 | 0 |
| BorderInCell | 10% | 256 | 14.1 / 14.8 / 16.3 | +0.4 | +0.6 | +0.1 | 10.3 (9.2) | 40 · 0 | 2 · 0 | 0 |
| BorderInCell | 50% | 1 | 13.7 / 14.3 / 15.0 | +0.2 | +0.3 | +0.0 | 10.0 (9.1) | 40 · 0 | 2 · 0 | 0 |
| BorderInCell | 50% | 16 | 14.1 / 14.5 / 15.4 | +0.6 | +0.5 | +0.6 | 10.3 (9.2) | 40 · 0 | 2 · 0 | 0 |
| BorderInCell | 50% | 256 | 14.6 / 15.3 / 16.8 | +1.0 | +1.1 | +0.6 | 10.7 (9.2) | 40 · 0 | 2 · 0 | 0 |
| BorderInCell | 100% | 1 | 13.7 / 14.2 / 16.3 | +0.1 | +0.1 | +0.0 | 10.2 (9.3) | 40 · 0 | 0 · 0 | 0 |
| BorderInCell | 100% | 16 | 14.1 / 14.7 / 16.0 | +0.5 | +0.5 | -0.3 | 10.4 (9.3) | 40 · 0 | 1 · 0 | 0 |
| BorderInCell | 100% | 256 | 14.2 / 14.8 / 16.4 | +0.7 | +0.7 | +0.0 | 10.9 (9.3) | 40 · 0 | 1 · 0 | 0 |
| BorderInCellExcel | 10% | 1 | 14.0 / 14.5 / 16.3 | +0.6 | +0.6 | +0.2 | 9.8 (9.1) | 40 · 0 | 2 · 0 | 0 |
| BorderInCellExcel | 10% | 16 | 14.6 / 15.6 / 16.6 | +1.0 | +1.7 | -0.1 | 10.1 (9.3) | 40 · 0 | 2 · 0 | 0 |
| BorderInCellExcel | 10% | 256 | 15.3 / 16.7 / 21.8 | +1.6 | +2.5 | +0.8 | 11.0 (9.2) | 40 · 0 | 3 · 0 | 0 |
| BorderInCellExcel | 50% | 1 | 14.4 / 15.1 / 17.3 | +0.9 | +1.1 | +0.9 | 10.1 (9.1) | 40 · 0 | 2 · 0 | 0 |
| BorderInCellExcel | 50% | 16 | 15.8 / 18.1 / 21.9 | +2.3 | +4.1 | +1.3 | 10.4 (9.2) | 40 · 0 | 2 · 0 | 0 |
| BorderInCellExcel | 50% | 256 | 18.2 / 21.0 / 27.5 | +4.6 | +6.8 | +1.6 | 11.3 (9.2) | 40 · 0 | 2 · 0 | 0 |
| BorderInCellExcel | 100% | 1 | 14.6 / 15.6 / 18.8 | +1.0 | +1.5 | +0.3 | 10.3 (9.3) | 40 · 0 | 0 · 0 | 0 |
| BorderInCellExcel | 100% | 16 | 15.4 / 17.3 / 20.3 | +1.8 | +3.1 | +0.9 | 10.6 (9.3) | 40 · 0 | 1 · 0 | 0 |
| BorderInCellExcel | 100% | 256 | 18.5 / 20.3 / 28.0 | +5.0 | +6.2 | +1.2 | 11.7 (9.3) | 40 · 0 | 2 · 0 | 0 |
| BorderLayer | 10% | 1 | 15.9 / 18.0 / 24.7 | +2.5 | +4.1 | +2.0 | 6.0 (9.1) | 0 · 1 | 0 · 1 | 250 |
| BorderLayer | 10% | 16 | 16.7 / 18.8 / 21.2 | +3.1 | +4.9 | +2.7 | 6.2 (9.3) | 0 · 1 | 0 · 1 | 303 |
| BorderLayer | 10% | 256 | 16.5 / 18.9 / 20.9 | +2.8 | +4.7 | +2.7 | 6.5 (9.2) | 0 · 1 | 0 · 1 | 307 |
| BorderLayer | 50% | 1 | 16.7 / 20.0 / 21.3 | +3.2 | +6.0 | +2.6 | 6.2 (9.1) | 0 · 1 | 0 · 1 | 336 |
| BorderLayer | 50% | 16 | 23.3 / 27.0 / 29.7 | +9.8 | +13.0 | +8.3 | 6.3 (9.2) | 0 · 1 | 0 · 1 | 1143 |
| BorderLayer | 50% | 256 | 23.7 / 28.4 / 37.9 | +10.1 | +14.2 | +8.7 | 6.7 (9.2) | 0 · 1 | 0 · 1 | 1194 |
| BorderLayer | 100% | 1 | 14.0 / 14.7 / 16.4 | +0.4 | +0.6 | +0.5 | 6.2 (9.3) | 0 · 1 | 0 · 0 | 60 |
| BorderLayer | 100% | 16 | 25.8 / 30.9 / 37.0 | +12.2 | +16.7 | +10.0 | 6.5 (9.3) | 0 · 1 | 0 · 1 | 1504 |
| BorderLayer | 100% | 256 | 26.1 / 31.2 / 37.2 | +12.6 | +17.1 | +9.1 | 6.9 (9.3) | 0 · 1 | 0 · 1 | 1594 |
| BorderLayerPerRow | 10% | 1 | 17.0 / 19.1 / 22.4 | +3.6 | +5.2 | +0.8 | 9.5 (9.1) | 0 · 40 | 0 · 2 | 279 |
| BorderLayerPerRow | 10% | 16 | 17.3 / 20.0 / 23.0 | +3.7 | +6.1 | +0.7 | 9.9 (9.3) | 0 · 40 | 0 · 2 | 305 |
| BorderLayerPerRow | 10% | 256 | 17.3 / 19.3 / 22.2 | +3.6 | +5.1 | +0.7 | 9.9 (9.2) | 0 · 40 | 0 · 2 | 307 |
| BorderLayerPerRow | 50% | 1 | 20.1 / 23.6 / 26.5 | +6.6 | +9.6 | +1.0 | 9.9 (9.1) | 0 · 40 | 0 · 2 | 773 |
| BorderLayerPerRow | 50% | 16 | 22.9 / 27.9 / 30.5 | +9.4 | +13.9 | +1.5 | 10.3 (9.2) | 0 · 40 | 0 · 2 | 1170 |
| BorderLayerPerRow | 50% | 256 | 23.3 / 27.7 / 34.8 | +9.7 | +13.5 | +1.7 | 10.6 (9.2) | 0 · 40 | 0 · 2 | 1195 |
| BorderLayerPerRow | 100% | 1 | 20.5 / 24.3 / 26.8 | +6.9 | +10.2 | +1.3 | 9.9 (9.3) | 0 · 40 | 0 · 0 | 840 |
| BorderLayerPerRow | 100% | 16 | 25.4 / 31.3 / 44.9 | +11.8 | +17.1 | +1.7 | 10.2 (9.3) | 0 · 40 | 0 · 1 | 1553 |
| BorderLayerPerRow | 100% | 256 | 25.7 / 27.1 / 41.4 | +12.2 | +13.0 | +2.8 | 10.4 (9.3) | 0 · 40 | 0 · 1 | 1597 |

### The in-cell variants (sweep D, each against the baseline of its own share and N)

| Share | N | RowComponent fling frame p50 / p95 / max | BorderInCell Δ p50 | BorderInCellExcel Δ p50 (render · layout · paint) | BorderInCellExcelBox Δ p50 (render · layout · paint) | Column change p50: base · InCell · Excel · Box |
|---|---|---|---|---|---|---|
| 10% | 1 | 14.8 / 15.4 / 16.9 | +0.0 | +0.4 (+0.2 · +0.1 · +0.2) | +0.2 (+0.2 · +0.1 · +0.1) | 10.3 · 9.8 · 9.9 · 11.6 |
| 50% | 16 | 14.9 / 15.4 / 17.8 | +0.5 | +1.6 (+0.2 · +0.5 · +0.9) | +2.3 (+0.0 · +1.1 · +1.2) | 10.4 · 10.2 · 10.3 · 12.5 |
| 50% | 256 | 14.5 / 15.1 / 16.0 | +1.1 | +4.7 (+0.8 · +2.8 · +1.1) | +4.0 (+0.8 · +1.6 · +1.7) | 10.4 · 10.6 · 11.3 · 23.2 |
| 100% | 1 | 14.9 / 15.5 / 17.1 | +0.1 | +0.8 (+0.1 · +0.1 · +0.5) | +0.2 (+0.1 · +0.1 · -0.1) | 10.3 · 10.1 · 10.0 · 11.7 |
| 100% | 16 | 14.9 / 15.4 / 15.6 | -0.0 | +1.1 (-0.0 · +0.2 · +0.9) | +2.7 (-0.1 · +1.3 · +1.4) | 10.5 · 10.4 · 10.4 · 12.5 |
| 100% | 256 | 14.5 / 15.2 / 15.6 | +0.7 | +4.5 (+0.8 · +2.4 · +1.3) | +4.2 (+0.7 · +1.8 · +1.7) | 9.7 · 10.6 · 11.4 · 21.7 |

### Where the cost of Excel's geometry comes from

**It scales with the number of distinct lines, not with the elements.**

- `BorderInCellExcel` adds no element and no inline style. The edges are resolved per row outside the
  render, and a cell indexes an interned class.
- Its extra cost over `BorderInCell` (sweeps C and D) grows with N:
  - +0.2 to +0.9 ms at N 1;
  - +0.7 to +1.7 ms at N 16;
  - +3.6 to +4.3 ms at N 256 on 50 to 100% of the cells, and +1.2 ms at 10%.

The split at N 256 and 50 to 100% is 0.8 to 1.2 ms of render (the longer class attribute), 2.2 to
2.8 ms of style recalculation, and 1.1 to 1.5 ms of paint.

- **Why style recalculation grows.** With 256 random lines on 50 to 100% of the cells, almost every
  painted cell matches a different set of rules, so the browser shares no computed style between
  cells. Each cell resolves four `var()`-fed background lists and parses its gradients.
- **Why paint grows.** It rasterises a different gradient for each distinct dash pattern and colour.
- **Why N 16 is cheap.** The same work is shared, and the mode costs +1.0 to +2.3 over the baseline.
- **The Box variant does not remove the cost.** It takes out custom properties and per-cell
  gradients, but it moves the cost into positioned pseudo-elements, inset shadows and shadow
  combinations: +4.0 to +4.2 at N 256, and +2.3 to +2.7 at N 16.
  - Its sheet grows with every new combination, so a column change costs 21 to 23 ms at N 256.
  - It is cheaper only where every line is solid: +0.2 at N 1.

**Is the worst case realistic? No.** Half or all of the visible cells, each with one of 256 line
styles and colours, with neighbours that differ, is a sheet nobody makes. The bench also cycles
through the thirteen styles evenly, so ten lines in thirteen are dashed. Real sheets use a few
styles, mostly thin and black, with outlines and totals in medium, thick or double. Those run along
rows and columns. N 1 to 16 is the realistic range.

### The geometry, against the eleventh run's case 9

`results/20261001-geometry/profiles.txt` has the pixels across each box's bottom and right edge.

- **At device scale 1 (100%), both Excel modes are exact for all thirteen styles:**
  - thin on the gridline;
  - medium on the gridline and the pixel above it;
  - thick one pixel above it and one below;
  - double as two lines either side of a white gridline;
  - the dash patterns 2/2, 3/1, 8-3-3-3 and 8-3-3-3-3-3, the medium ones on two rows.

  Only the dash phase differs from Excel's, and slanted dash-dot is one pattern over both rows.
- **At device scale 1.5 (150%, under CDP device-scale emulation), neither is exact:**
  - `BorderInCellExcel` keeps its lengths in device pixels through a resolution media query, as
    Excel's are. Its 1-px parts and dashes are crisp and in place, but a 2-px part (medium, and the
    upper part of thick and double) comes out about 1.5 device pixels, with an antialiased row.
  - `BorderInCellExcelBox` is worse: Chrome computes a 0.667px or 1.333px border as `1px`.

  A real browser zoomed to 150% has not been checked, nor Windows. Ticket 49's pixel tests at 150%
  are where this is settled.
- **`BorderLayer`** places medium, thick and double as Excel does, by backing a 2- or 3-px line up
  one pixel. Its dashes are CSS's.
- **`BorderInCell`** is exact for thin and medium, but thick and double sit a pixel high. A wider right
  border also moves the text.

### Recommendation for Borders: Excel's geometry first, cost second

**`BorderInCellExcel`.**

- **Geometry.** It is the only mode exact for all thirteen styles at 100%. It changes no row's height,
  moves no text, and paints nothing outside the row. The rows stay the memoisation boundary.
- **Cost.** In the realistic range (N 1 to 16) it adds +0.4 to +2.3 ms to a fling frame (p95 up to
  +4.1). A slow scroll step adds -0.1 to +1.3 ms in C, and up to +2.0 in D, which ran under more
  load.
- **Worst case.** +5.0 ms at the median (+6.8 at p95), at 50 to 100% share and N 256. Headless, that
  puts the median at 18.2 to 19.2 ms, over 16.6.
- **The trade, plainly.** Exact geometry is cheap for sheets as people make them. It is not free for a
  sheet where most visible cells carry different lines. There it costs about 4 ms a frame more than
  lines inside each cell's box (`BorderInCell`, +0.7 to +1.1), and `BorderInCell` is 1 px off for thick
  and double.

**The layer is too costly.**

- `BorderLayer` costs +2.5 to +12.6 ms on a fling frame, and +2.0 to +10.0 on a slow scroll step,
  because every run moves with the painted slice. The exception is "All Borders" (100%, N 1): there
  each gridline is one run, 60 in all, and the cost is +0.4 and +0.5.
- `BorderLayerPerRow` brings a slow scroll step down to +0.7 to +2.8, but a fling frame costs the same
  as the layer's, +3.6 to +12.2. Each vertical line is cut per row (840 elements for "All Borders"
  against the layer's 60).
- Both scale with the elements: up to about 1600 for 800 cells.
- A layer's one advantage: a border change renders no row. A column change is 6 ms against about 10.

**To try in ticket 47, to keep the geometry and lose most of the cost:**

1. **Draw the solid lines (thin, medium, thick's upper part) as the cell's own border colour and
   width.** The Box variant shows this costs what `BorderInCell` costs (+0.2 at N 1).
2. **Keep only dashes, double and the pixel past the gridline as background layers**, read by a
   static rule scoped by a marker class, so that a cell with only solid lines resolves no `var()`.
3. **Intern per (side, style, colour)**, as both modes already do. Do not intern per combination of
   four sides: the Box variant's shadow combinations show how the sheet then grows.

That hybrid was not measured.

### Not measured

- **Hardware, platforms and runtimes:** real hardware, Windows, a real browser's 150% zoom, scale 2
  (a Retina display), Blazor Server and AOT.
- **`CellFormatClasses` with `BorderInCellExcel`** on the same cells: a Fill and a border background
  both on one cell.
- **The hybrid** above.

### Seen on the way

`BenchRow` (the first page's `RowComponent`) does not record what it rendered at mount. A mounted
row therefore renders once more on the next parent render. That adds a little to its slow-scroll
numbers, which nothing in this ticket depends on. The new `FormatRow` records it.
