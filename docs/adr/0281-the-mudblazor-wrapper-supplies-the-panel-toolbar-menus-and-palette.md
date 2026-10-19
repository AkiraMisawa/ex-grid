# The MudBlazor Wrapper supplies the chart's panel, toolbar, menus and palette

*Decided with the user, 2026-10-19 (M4 grilling Q2 and Q3). ADR-0250 put `ExChart.MudBlazor` into the
first version; ADR-0258 (M3-Q5) settled that its panel sits beside or below the grid, never over it.*

## What it supplies

1. **The chart panel**: beside or below the grid, several charts as `MudTabs`, which the selection
   bridge's command can open directly (ADR-0258).
2. **A toolbar**: Chart Filters' range typed in MudBlazor's inputs (a date picker for a date axis),
   the axis switch where the Consumer allows one (M1-Q3), and the reset of the range (ADR-0273).
3. **The download menu** (CSV, SVG, PNG; ADR-0282) **and the chart's context menu**, as `MudMenu`.
4. **A stylesheet** mapping ExChart's Visual Tokens onto MudBlazor's palette variables, following the
   theme and dark mode, as `mud-ex-grid.css` and `mud-ex-pivot.css` do.

**The legend, the tooltip and the plot are the Built-in ones.** Chrome renders and calls back; the
core decides meaning (spine rule 4), so swapping the Wrapper in changes how the chart looks and not
what it does. Redrawing the legend as `MudChip`s or the tooltip as a `MudTooltip` would give the
tooltip a second path beside the pointer entry (ADR-0255), and two paths drift.

## Which MudBlazor colours the colour slots take

- **Slots 1 to 3 take `Primary`, `Secondary` and `Tertiary`.** A theme's own colours lead the chart.
- **Slots 4 to 10 keep ExChart's own default palette** (ADR-0256).
- **`Error`, `Warning`, `Success` and `Info` are never a series' colour.** On a screen of profit and
  risk a red series reads as an error or a loss, and a green one as good news, whatever it is. They
  stay for what they mean: a refusal, a note that something could not be drawn.

## Considered

- **The stylesheet only**, leaving panel and toolbar to the Consumer: every Consumer rebuilds the
  same panel, and the bridge's command has nowhere to open a chart.
- **Legend and tooltip as MudBlazor components**: a second rendering path, rejected above.
- **All ten slots from MudBlazor's palette**: the semantic colours land on series.
- **No slot from MudBlazor's palette**: the chart ignores the theme a Consumer chose.
