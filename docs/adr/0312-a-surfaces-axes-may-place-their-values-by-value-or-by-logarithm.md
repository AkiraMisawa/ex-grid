# A surface's axes may place their values by value or by logarithm

*Decided while the user was away, 2026-10-20, under their standing instruction to pick a reasonable
default, record it in an ADR and carry on; the user asked for log axes on the surface for the first
version. It extends [ADR-0274](0274-a-3d-surface-takes-a-grid-and-is-turned-zoomed-and-sliced.md) with
the scales [ADR-0290](0290-a-scatter-is-a-series-of-observations-each-a-row.md) gave a 2D chart; the
user may revisit it.*

A grid's lines were evenly spaced, one per value: 1W, 1M, 3M … 10Y at equal steps. That is how a
market quotes a volatility surface, and Bloomberg's own volatility surface pages show the tenors that
way (from memory). It stays the default. But a surface over strikes in currency, over years, or of a
variance that spans decades needs its positions placed by their value, or by their logarithm.

## Decisions

- **`SurfaceAxis.Scale`**: `Categories` (the default for a grid: evenly spaced, as before),
  `Proportional` or `Logarithmic`. A scale placed by value takes numbers that rise along the axis.
  A logarithmic one also takes them above zero. Anything else is refused by name: a value that is not
  a number, one that does not rise, one at or below zero. Refused, not quietly respaced, because the
  axis would put a tenor where it is not, as ADR-0290 refuses it on a 2D axis across.
- **`SurfaceDefinition.ZScale`**: `Proportional` (the default) or `Logarithmic` for the values. A value
  at or below zero, or a `Centre` at or below zero (ADR-0310), keeps it proportional, and a Note says
  why. That is what a 2D chart's vertical axis does (ADR-0290). A vertical scale that falls back is
  shown, and the axis across is refused, because an axis across that fell back would move every
  position.
- **The faces, the walls and the colour bar follow the vertical scale.** The vertical position is
  the logarithm on a logarithmic scale. The walls carry decade ticks, written as the values are. A
  face's colour is the mean of its corners' logarithms.
- **The floor's lines follow the axis' scale.** A grid keeps a line at each of its values, because
  those are the lines its faces are drawn between. Evenly spaced, they are labelled where twelve fit.
  Placed by value, a value is labelled where its label does not crowd the last one: a twelfth of the
  floor apart. So 1W, 1M and 3M on a ten-year axis share one label, and on a logarithmic one each has
  its own. A mesh has no lines of its own, so its floor has them at round numbers, 1-2-5 per decade on
  a logarithmic axis (`TickLayout.LogSteps`). They are written through the axis' format when the
  round number is a value of the axis' own type.
- **A function sampled on a logarithmic axis is sampled evenly in the logarithm**, as a 2D sampled
  series is (ticket 51). Sampling from zero or below on one is refused.
- **The slice keeps the scales.** Along an axis placed by value it is a chart of numbers on that
  scale, and its vertical axis is logarithmic when the surface's is.
- **.NET places every point.** The 3D entry is handed fractions of the floor and values on the vertical
  scale, and only projects them (ADR-0255).
