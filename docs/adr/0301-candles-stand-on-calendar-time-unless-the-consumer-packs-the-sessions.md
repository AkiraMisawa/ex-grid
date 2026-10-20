# Candles stand on calendar time unless the Consumer packs the sessions

*Decided with the user, 2026-10-20 (grilling of candlesticks and error bars, Q3). Proposed first was
the packed axis as the default; the user found calendar time more natural, and asked for both.*

Between Friday's close and Monday's open no one trades, and intraday bars spend fifteen hours a night
the same way. Excel's date axis keeps every calendar day, so weekends are gaps; its Text axis packs
them. Trading screens (TradingView, Bloomberg) pack them.

- **By default a Price Range stands on the Dates axis as calendar time**, as every other series in
  ExChart does, and a candle is as wide as its own period: a daily candle spans its day, centred on
  it. A weekend is then a gap, and the gap is true — time passed, and a price can open away from
  where it closed.
- **The Consumer may declare the sessions packed** (`ChartAxis.Packed`): each row with a value is
  one slot, evenly spaced, in the data's order, and ticks are still the real dates. The axis title
  says so (`Trading days (closed days packed)`, or the Consumer's own title with the same words), and
  so do the Notes, so a reader never takes the even spacing for
  calendar time. On a packed axis every series in the chart takes only the candles' positions; a
  point at another position is refused by name, because there is nowhere true to put it.
- **Switching is the Consumer's.** The chart holds no View State
  ([ADR-0272](0272-a-chart-holds-no-view-state-it-raises-what-the-user-changed.md)); the Showcase
  carries a toggle.

## Rejected

- **Packed by default**: dates would mean calendar time on one chart and sessions on the next.
- **No default**: every Consumer would answer a question most of them do not have.

## Amended in implementation, 2026-10-20

What the first implementation (PR #192) found, recorded rather than left in the code:

- **The Notes carry the packing too.** ExChart draws no axis title of its own yet; the title is
  computed (`ChartScene.XTitle`) for a Consumer who draws one, and the Notes under the chart say
  `Dates are packed: …` so the reader is told on every chart.
- **Three refusals the decision implies, now named.** Only a Dates axis packs (a Numbers axis has
  no sessions to leave out); a packed axis needs a Price Range to take its sessions from; and a
  function series is refused on it, since it has no values between the sessions.
- **A Proportional scale in the View unpacks the axis.** The Consumer's switch is still
  `ChartAxis.Packed`; the View's scale toggle (ADR-0272) asks for proportional spacing in so many
  words, and packing is the opposite of that, so the request wins. The chart holds no state of its
  own either way.
