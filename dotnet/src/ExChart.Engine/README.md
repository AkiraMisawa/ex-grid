# ExChart.Engine

The chart engine of ExChart, with no UI and no dependency. It takes a **Chart Definition** — series in
the data's own terms, each with its **Series Kind** and **Appearance Keys** — and decides everything a
chart shows that is not a pixel of paint:

- **Series Kind**: a Level is points joined only to their neighbours in the data; a Rate over an
  interval and an Amount per period are steps with filled and open ends, an amount drawn per year. A
  missing value is a gap, never a zero.
- **Axes**: proportional (dates, numbers, declared positions) or categories; ticks and labels that are
  left out rather than overprinted; zeros of a left and a right axis at the same height.
- **Thinning**: past the plot's device pixels, each pixel keeps a series' first, last, minimum and
  maximum point.
- **Interpolated series** are the Consumer's: handed in as points or as a function the engine asks at
  about one position per pixel, in Segments that name their method.
- **CSV**, wide and long, invariant, every digit.

`ExChart` draws what this engine lays out.
