# A scatter is a series of Observations, each a row, on two value axes

*Decided with the user, 2026-10-19, in the ExChart project's scatter grilling (Q1, Q3, Q4, Q5, Q6). The
user put scatter into the first version (2026-10-19: "it makes patterns easy to see, and lines can be
drawn over it"; an AI asked to analyse CDS and regress them would chart its result with this
component). This moves scatter out of ADR-0250's "Later".*

A spread against a leverage, one point a name, is how a credit desk sees whether a relation holds and
which names sit off it. Two names may share a leverage, and nothing orders them by it — yet every
series ExChart drew until now was a function of its x: points in increasing x, joined to their
neighbours.

## Observation, a fourth Series Kind

*Q1.* **Observation** joins Level, Rate over an interval and Amount per period (ADR-0253):

| Series Kind | Means | Drawn as | Axis from zero |
|---|---|---|---|
| **Observation** | one thing measured on two quantities (a name's leverage and spread) | a marker, never joined | no, unless the series declares so |

- **Its points are neither ordered nor unique in x.** The rule that points rise in x (ADR-0253's
  "a line joins only neighbours") is a rule of the joined kinds; an Observation series is not refused
  for it.
- **Each point may carry a Row Key and a name.** The tooltip says the name; a click raises the Row Key,
  and the ready-made bridge goes to that row (ADR-0258). A point without a Row Key is not clickable,
  and is not guessed from its x.
- **A missing x or y is no point**, never a point at zero (ADR-0253's gap rule).
- **It lives on the same chart as every other kind.** A fitted line over a scatter is an ordinary Level
  series on the same axes (ADR-0291), so the legend, the Notes, the CSV and the files are shared, not
  built a second time.

Rejected: a separate scatter definition beside the Chart Definition. It would hold a legend, Notes and
files of its own, and need a bridge to put a line over its points.

## Two value axes

*Q1, Q5.* A chart that holds an Observation series reads its x axis as a value axis like its y: Numbers
or Dates, never categories, each written in its column's format (ADR-0251).

- **Neither axis includes zero by default.** A scatter is read for its pattern and its slope; spreads
  of 180 to 320bp drawn from zero squeeze the pattern into a band at the top. An axis that does not
  reach zero keeps the break mark at its foot (ADR-0253), so the reader is told. A series may declare
  that it reads from zero.
- **Either axis may be logarithmic**, the x and the y on their own: spreads and sizes are read on a log
  scale. A series with a value at or below zero on a log axis is **refused by name**, never drawn
  without it. Log scale is not limited to Observation; a Level may take it too.

## Past the pixels, marks on the same pixel are merged

*Q3.* ADR-0254's minimum and maximum per pixel column are a line's: they keep a line's shape. A
scatter's shape is the set of pixels its markers cover.

- **When two or more of a series' markers centre on the same device pixel, one marker is drawn.**
  Every occupied pixel keeps its marker, so no outlier is lost, and the drawing is the one the full
  series makes.
- **The tooltip on a merged mark says how many it holds** ("XYZ Corp and 13 more"), never one point as
  if it were the only one (ADR-0254).
- **The CSV and the files carry every point.**
- Accepted: how many points overlap is not visible in the drawing. A density drawing is a later Series
  Kind, not a switch on this one.

Rejected: switching to a density drawing (hexagonal bins, a heat map) past a count — it loses each
point's row and the outlier's mark; translucent markers alone — they thin nothing.

## The pointer and the drag

*Q4.*

- **The tooltip shows the one point nearest the pointer**, within a marker's reach, and nothing
  farther away. ADR-0270's "every series at the pointer's position" is a line's; on a scatter many
  points share an x. The pointer entry (ADR-0255) reports the nearest point only when it changes, from
  the marks' positions handed to it with the drawing; it measures nothing.
- **A drag draws a rectangle and narrows both axes**, raised as an event like ADR-0273's range; the
  chart holds no View State (ADR-0272).
- **A click on a point goes to its row** (ADR-0258). A rectangle does not select rows in the first
  version: the drag would then have two meanings and need a switch between them.

## Shown

*Q6.* A Showcase of its own, **CDS spread vs leverage** (the fourth, once CDS curves, Curves and Rates became one; ADR-0280 is amended by the shipping work): synthetic names from a fixed seed in a
grid beside the scatter, the least-squares and the Theil–Sen line over it, a distressed name pulling
only the first, a log y axis to switch on, a click going to the row, the fits' coefficients in the
Notes. An eighth page of the Chart section, **Scatter and fitted lines**.
