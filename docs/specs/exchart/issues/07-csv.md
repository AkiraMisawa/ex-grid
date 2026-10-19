# 07: The chart's data as CSV, wide and long

Status: done

**Why:** [ADR-0254](../../../adr/0254-past-the-plots-pixels-a-series-is-thinned-to-its-minimum-and-maximum.md) (files carry every point), the proof of concept's §3.8.

- [x] Wide (a row per x, a column per series and kind) and long (series, x, value, kind — quoted, interpolated, extrapolated — and method)
- [x] What the chart shows: the series and range after Chart Filters; a sampled series as the samples drawn
- [x] Invariant, unformatted, every digit, UTF-8 with a byte-order mark; a missing value is an empty field, never a zero
- [x] Every point, never the thinned ones; what the Chart Filters hide is left out

**Blocked by:** 02.
