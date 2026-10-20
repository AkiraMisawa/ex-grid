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
- **It says so.** The axis title gives the period (`Weekly (merged from daily to fit)`), and the
  tooltip the period and its count (`2026-10-05 – 2026-10-09, 5 rows`). The CSV and the files carry
  every row. Narrowing the range brings the rows back as they are.
- **A packed axis merges on the same calendar boundaries.**
- **A Consumer may fix the period** (`ChartSeries.MergeTo`, Bloomberg's way: its user picks the
  period, 1 minute to 1 year). A fixed period that does not fit is never squashed into slivers: the
  plot draws nothing for the series and says `2,450 daily bars do not fit; narrow the range`.

## Rejected

- **Merging every n bars per pixel**: an "eleven-day bar" no market quotes and no reader can name.
- **A high-low line per pixel** (ADR-0254's shape): the open and the close are lost.
