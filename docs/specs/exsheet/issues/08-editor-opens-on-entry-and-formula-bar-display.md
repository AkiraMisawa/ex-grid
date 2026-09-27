# 08: The editor opens on the Entry; the Formula Bar shows it

Status: done

**What to build:** Two ADR-0051 pieces that need no typing-time traffic. A Consumer supplies the text the Cell
Editor opens on, and ExSheet supplies the Entry, so F2 on a Formula's cell shows `=A1*2`. The
Formula Bar band, inside the root above the header and switched on by the Consumer, shows the Name
Box (the Consumer's label: `D200`) and the Focus cell's text. On a plain ExGrid it shows the full
value, which discharges ADR-0016's display. Its height comes from the Grid Metrics.

**Blocked by:** 02

- [x] Editing a Formula's cell opens on the Formula, not the Value
- [x] The Formula Bar follows the Focus and shows the Entry; the Name Box shows the address
- [x] On a plain ExGrid it shows the full value behind `####` (ADR-0016)
- [x] Switched off, there is no band and the geometry is unchanged
- [x] The rows take what the band leaves, with no stylesheet/C# pairing (ADR-0027/0028)

## Comments

The core half, 2026-09-27. Three declarations, each off by default:

- `EditorTextOf` (`Func<TRow, GridColumn<TRow>, string?>?`): the text an edit opens on, per
  cell. F2 and a double click open Caret on it; a typed character still opens Overwrite with that
  character (DC-16). A null answer falls back to the value, as before.
- `ShowFormulaBar` (`bool`): the band, inside the root above the header. It is written after the
  scroller in the markup, so the Cell Editor stays the first editor surface the key listener finds,
  and it stands in the root's `padding-top`. Its height is `GridMetrics.FormulaBarHeightPx` (the
  header's) and comes out of `ViewportHeight`: the scroller is what the band leaves, and a Viewport
  that cannot hold the band and the header is refused by name. Popovers are placed below it.
- `NameBoxLabel` (`Func<CellPosition, string?>?`): the Name Box's label for the Focus. Without it
  the Name Box is empty, because the grid does not name cells itself.

The bar's text is the opening text where one is supplied, and otherwise the full value in the
current culture. It is never the `####` and never the column's rounding format (DC-21). Layer 1:
`GridMetricsTests`; layer 2: `FormulaBarDisplayTests`.

What remains:

- ExSheet's wiring (the Entry as the opening text, `D200` as the label) is the ExSheet stream's.
- The Chrome seam for the bar's two fields (ADR-0051's consequences) is not built: the built-in
  markup paints them under every Chrome. `ExGrid.MudBlazor` substitutes nothing here yet.
- Layer 3: the band above the header as painted, under both Chromes, at both hosts; the Fill-height
  case (the root's padding and the scroller's flex share) as the browser lays it out.

2026-09-27, ExSheet's wiring: `EditorTextOf` answers the Entry (`Sheet.GetEntryText`: the Formula,
or the constant written as it would be typed under the Sheet's culture), `ShowFormulaBar` is on by
default, and `NameBoxLabel` answers the Focus's A1 address. Layer 2: `FormulaBarWiringTests` (F2
opens on `=A1*2` while the cell shows 42; the bar and the Name Box follow the Focus; a `de-DE`
constant opens as typed there).
