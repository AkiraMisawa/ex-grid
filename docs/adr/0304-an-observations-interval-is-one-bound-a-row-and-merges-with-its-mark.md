# An Observation's interval is one bound a row, and merges with its mark

*Decided while the user was away, 2026-10-20, under their standing instruction to pick a reasonable
default, record it in an ADR and carry on. It completes
[ADR-0303](0303-an-interval-belongs-to-a-series-and-says-what-it-means.md) for the Observation
([ADR-0290](0290-a-scatter-is-a-series-of-observations-each-a-row.md)); the user may revisit it.*

ADR-0303 gives an Observation an interval on each of its two quantities. A Level's bounds are found
by position, but an Observation's points are neither ordered nor unique across: two names can share
a leverage, each with its own spread and its own interval.

- **Its bounds are one a row, in the points' order.** `Interval` (vertical) and `XInterval`
  (horizontal) each hand in exactly one bound per point, the i-th belonging to the i-th point, and
  each bound repeats its point's position across. A count or a position that does not match is
  refused by name, because a bound shifted by one row would sit on its neighbour and look right.
- **Whiskers only.** A band joins neighbours, and an Observation has none; a band is refused.
- **A mark that stands for several points carries the union of their intervals** — the lowest lower
  and the highest upper on each quantity — as ADR-0303's thinning does, so no interval is dropped
  where marks merge (ADR-0290). The tooltip names how many points the mark stands for, as before.
- **The axes take the bounds**, across and up, so a whisker is never cut by the plot's edge.
- **On a logarithmic axis a bound at or below zero keeps that axis proportional**, said by name in
  the Notes, as a value at or below zero is (ADR-0290). The same holds for a Level's bounds on a
  logarithmic vertical axis.
- **Each meaning is said where ADR-0303 says it**: the legend reads `Spread (y: 95% CI; x: ±1σ)`
  when the two differ and `Spread (95% CI)` when there is one, the Notes name each, and the tooltip
  gives each bound with its meaning. The CSV, a row per point (ADR-0290), adds a lower and an upper
  column for each quantity, named by the meaning when every series means the same, and otherwise
  named plainly with the meaning in a column of its own.

## Rejected

- **Bounds keyed by position**, as a Level's are: a position held by two points could not say whose
  bound it is.
- **Drawing the bound of the first point of a merged mark**: the others' intervals would be lost
  where they are widest.
