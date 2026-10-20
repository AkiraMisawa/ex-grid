# Past the plot's pixels, candles merge into the market's own periods

*Decided with the user, 2026-10-20 (grilling of candlesticks and error bars, Q5), after looking at
Bloomberg and Highcharts Stock.*

Ten years of daily candles are about 2,450 bars; on a 600-pixel plot that is a quarter of a pixel
each, and a hollow body needs about three to be told from a filled one. ADR-0254's minimum and
maximum per pixel would keep the wicks but lose the open and the close.

- **Past three device pixels a candle, consecutive candles merge on calendar boundaries**: daily
  into weekly, then monthly, quarterly and yearly, the first that fits. A merged candle's open is its
  period's first open, its high the highest high, its low the lowest low, its close the last close,
  and its volume the sum. Those are the market's own weekly and monthly bars, so nothing is drawn
  that the data does not hold, and no spike is lost (ADR-0254's rule, kept). Highcharts Stock groups
  the same way.
- **It says so.** The axis title gives the period (`Weekly (merged from rows to fit)`), the Notes
  say it under the chart (`Index: weekly candles, merged from rows to fit.`), and the
  tooltip the period and its count (`2026-10-05 – 2026-10-09, 5 rows`). The CSV and the files carry
  every row. Narrowing the range brings the rows back as they are.
- **A packed axis merges on the same calendar boundaries.**
- **A Consumer may fix the period** (`ChartSeries.MergeTo`, Bloomberg's way: its user picks the
  period, 1 minute to 1 year). A fixed period that does not fit is never squashed into slivers: the
  plot draws nothing for the series and says `Index: 2,450 rows do not fit; narrow the range.`

## Rejected

- **Merging every n bars per pixel**: an "eleven-day bar" no market quotes and no reader can name.
- **A high-low line per pixel** (ADR-0254's shape): the open and the close are lost.

## Amended in implementation, 2026-10-20

What the first implementation (PR #192) found, recorded rather than left in the code:

- **"rows", not "daily".** The examples first read `merged from daily` and `daily bars`. A row may
  be an hour or a week; the chart knows only that it is a row, so it says `rows`. A period the
  Consumer fixed is not said to be merged "to fit": the title reads `Weekly (merged from rows)` and
  the Notes `Index: weekly candles, merged from rows.`
- **The Notes carry the period**, since ExChart draws no axis title yet (`ChartScene.XTitle` is
  computed for a Consumer who draws one).
- **When not even yearly candles fit**, the series is drawn as nothing, as a fixed period that does
  not fit is, and the Notes say `Index: 12 yearly candles do not fit; narrow the range or widen the
  chart.` The rule against slivers holds whoever chose the period.
- **A merged candle spans its rows, not its whole period.** Its body is drawn from its first row to
  its last, and a click narrows the range to those rows. On a packed axis a closed day has no slot,
  so a period's calendar edges have nowhere to stand; on a calendar axis the difference is a
  holiday at a period's edge, and the range then holds exactly the rows the candle holds.
