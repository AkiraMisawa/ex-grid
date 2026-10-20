# A Price Range is one bar a row, refused where its values disagree

*Decided with the user, 2026-10-20 (grilling of candlesticks and error bars, Q1, Q2, Q4, Q7, Q8). The
user asked that charts common in finance take TradingView and Bloomberg as their reference.*

A candlestick says four things about one period: where it opened, how far it rose and fell, and where
it closed. None of the Series Kinds of [ADR-0253](0253-every-series-declares-its-series-kind-and-its-drawing-follows.md)
holds four values at one position, and a candle drawn from values that disagree — a high below the
open — still looks like a candle. Most libraries draw it as it is, with the wick inside the body,
and nobody notices.

**A Price Range is a fourth Series Kind**: one row is one bar, and the Consumer names its open,
high, low and close columns (Excel's order, `O, H, L, C`), and optionally a volume column.

| | |
|---|---|
| **A row whose values disagree** | refused, naming the row and the values: `the row of 2026-10-14: high 99 is below open 101`. The conditions are `low ≤ min(open, close)` and `max(open, close) ≤ high`. Never repaired to the min and max: that draws values the data does not have |
| **A row missing any of the four** | a gap at its date, `(no value)` in the tooltip; never a partial candle |
| **A series with no open column at all** | a High-Low-Close bar without a body, as Excel's |
| **A negative value** | drawn. WTI settled below zero in April 2020; a spread is negative |

## Rising and falling are told by shape first

A candle is rising when its own close is above its own open, and falling when below — never against
the previous close. A rising body is hollow and a falling body filled, so the two are told apart in
print and by a reader who does not see the colours. Colour lies on top: the rise and fall colours
are Visual Tokens (`--ex-chart-rise`, `--ex-chart-fall`), and by default both are the series' own
colour slot. Japan's red and blue, the West's green and red, and China's red rising are a Consumer's
or the MudBlazor Wrapper's setting, never the default. A candle whose open equals its close is one
horizontal line, and the tooltip says `unchanged`.

## The tooltip, the click and the value axis

- **The tooltip** gives the candle's period, its four values and its change from the previous
  candle's close: `2026-10-14 O 39,120 H 39,480 L 38,950 C 39,310 Chg +210 (+0.54%)`, with `—` where
  there is no previous candle. A merged candle ([ADR-0302](0302-past-the-plots-pixels-candles-merge-into-the-markets-own-periods.md))
  gives its period and how many rows it holds, and its change is from the previous merged close.
- **A click** on a candle that stands for one row goes to that row ([ADR-0258](0258-the-selection-bridge-follows-the-rows-it-was-made-from.md));
  on a merged candle it raises a request to narrow the range to its period, the event a drag across
  the plot raises ([ADR-0273](0273-the-user-narrows-the-range-by-dragging-across-the-plot.md)).
- **The value axis does not include zero** by default — a price is read by its shape, and an index
  between 38,000 and 42,000 drawn from zero is a flat line. An axis that does not reach zero shows the
  break mark, as a Level's does (ADR-0253).

## Volume is the bar's own column, in a panel below

A session's volume is not an Amount per period: ADR-0253 draws an amount per year of its interval,
and Monday's volume would be spread over the three days since Friday's close.

- **Volume is the fifth, optional column** of the Price Range, drawn as bars in a panel of its own
  below the candles, sharing their horizontal axis (Bloomberg's layout). A bar's height is the
  session's volume, undivided. Its axis always starts at zero.
- **A volume bar follows its candle's rule**: hollow when the candle rises, filled when it falls, in
  the same colour. TradingView colours volume against the previous close and its candles against
  their open, so a falling candle can stand over a rising volume bar; one rule means they never
  disagree.
- Merged candles sum their volume. The tooltip carries it.

## Rejected

- **Error bars as a Series Kind of their own**: see [ADR-0303](0303-an-interval-belongs-to-a-series-and-says-what-it-means.md).
- **Drawing disagreeing rows as they are**, the common default: a wick inside the body is quietly wrong.
- **Volume overlaid on the price panel with an axis of its own** (TradingView's default): two scales
  in one area, and where a price line crosses a volume bar means nothing.
- **Green and red by default**: colour alone fails in print and for colour vision, and it is the
  reverse of the Japanese convention.
