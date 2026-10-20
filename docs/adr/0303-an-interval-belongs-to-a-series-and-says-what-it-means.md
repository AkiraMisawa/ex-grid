# An interval belongs to a series and says what it means

*Decided with the user, 2026-10-20 (grilling of candlesticks and error bars, Q1 and Q6), after looking
at Excel, Plotly, TradingView and Bloomberg.*

A whisker on a 2-year rate of 3.12% means one thing as a 95% confidence interval, another as ±1σ,
and another as the bid–ask — and the picture is the same. Excel's Standard Deviation and Standard
Error error bars are worse: they are computed from the plotted values' own spread, so every point
carries the same whisker, which looks like each point's error and is not.

- **An interval is not a series.** It belongs to the points of a Level (vertical) or an Observation
  (vertical and horizontal), so it shares that series' legend, Notes, tooltip and CSV. An interval
  apart from the line it qualifies would have to be kept aligned with it by hand, and a mismatch is
  never seen.
- **The Consumer hands in the bounds**, a lower and an upper column, so an asymmetric interval is
  drawn as it is. **ExChart computes no width**: no standard deviation, standard error, percentage or
  constant.
- **Its meaning is required**: `95% CI`, `±1σ`, `Bid–Ask`. It is shown in the legend, in the tooltip
  (`2Y: 3.12% [3.08%, 3.16%] 95% CI`), in the Notes, and in the CSV's column names. An interval
  without one is refused.
- **A lower bound above its upper is refused**, naming the position. A value outside its own
  interval is drawn — a trade can print outside the bid–ask.
- **A point missing either bound has no interval there**, and the tooltip says `(no interval)`.
- **On a Level an interval is drawn as whiskers or as a band**, an appearance choice
  ([ADR-0256](0256-a-series-look-is-chosen-by-three-appearance-keys.md)): the data is the same. A
  band suits a time series (financial screens draw intervals as named bands, Bollinger's among them),
  whiskers suit points. A band joins only neighbours and breaks at a point with no interval, as a
  line does (ADR-0253).
- **Past one point a device pixel, whiskers become the band**, thinned to the lowest lower and the
  highest upper per pixel column, so no interval is dropped where markers are (ADR-0254).
- **On a log axis a bound at or below zero is refused** by name, as a value is. *(Amended 2026-10-20 by
  [ADR-0304](0304-an-observations-interval-is-one-bound-a-row-and-merges-with-its-mark.md): the
  logarithmic axis, a View request since ADR-0290, is what gives way: it stays proportional and the
  Notes name the bound, as they name a value. Nothing is drawn wrong either way.)*

## Rejected

- **Excel's options** (fixed value, percentage, standard deviation, standard error): the last two
  draw the wrong thing plausibly.
- **Computing ± a constant or ± a percentage**: a Consumer has both in one line, and then hands in
  the bounds it means.
- **Error bars as a Series Kind of their own**, as above.
