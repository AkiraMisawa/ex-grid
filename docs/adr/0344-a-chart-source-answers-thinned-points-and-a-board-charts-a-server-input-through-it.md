# A Chart Source answers thinned points, and a Board charts a server input through it

*Taken in the user's absence, 2026-10-21, under their standing authorization (2026-10-20) for a fork
only they could settle: a reasonable default, recorded here, which they may overturn. The user asked
that the chart [ADR-0343](0343-a-board-over-a-source-held-on-a-server-hands-it-the-query-and-the-narrowing.md)
refused be built. This settles the shape [ADR-0322](0322-a-chart-reads-its-data-through-a-chart-source-that-thins-where-the-data-lives.md)
left to be "settled when it is built".*

## Context

ADR-0322 decided that a chart reads its data through a Chart Source, which answers "this column, from
x0 to x1, at N pixels" with each pixel's first, last, lowest and highest point and its count, and that
where it thins is the source's business. It did not say what the answer is made of, how a Chart Spec
reads one, or who chooses N. ADR-0343 refused a Board chart over an input held on a server until it
was built.

## Decisions

- **The seam is in ExChart.Engine and references nothing** (ADR-0322's default): `IChartSource` with
  one call, `ThinAsync(ChartSourceQuery)`. The query names the data, the column across (`X`), the
  column of values (`Y`), what the axis places (dates or numbers), the range (`From`, `To`, in the
  axis' own positions: a date's day number, a number; null for the data's own end) and `Pixels`.
- **The answer is a table of the points kept, not a new kind of thing.** `ChartSourceAnswer` holds
  the version of the data it was read from, an `IChartData` with the columns `X` and `Y` holding the
  rows kept, in order across, and the `PixelSummary` of each pixel that held more than one point. A
  Chart Spec reads that table as it reads any other, so every refusal it says about data — a value
  that is no number, a date with a time of day — is said of what the source answered.
- **Thinning is ADR-0254's, done on the data's positions:** the range is cut into `Pixels` equal
  columns, and each keeps its first, last, lowest and highest row, never one picked by LTTB (ADR-0322).
  A row with no value is kept, so a gap stays a gap. `ChartSource.Thin(IChartData, ChartSourceQuery)`
  is the reference implementation, and the specification a server's source answers as (ADR-0322); it
  is what a source over data held in the process uses (`ChartSource.Over`).
- **The tooltip says what the source thinned.** A series read from a Chart Source carries the
  answer's summaries (`ChartSeries.Thinned`), and the chart's own Thinning counts through them: a
  device pixel that holds kept points of several source pixels says the range and the count of all
  the rows under it, not of the points it was sent.
- **A chart read from a Chart Source holds only what was drawn, and says so.** Its CSV and its table
  would be the points kept, which look like the data and are not (spine rule 1): the chart refuses to
  save a CSV, and its table says, in its place, that the rows are where the data lives. The pictures
  (SVG, PNG) save as drawn.
- **What thinning cannot stand for is refused by path, before any source is asked.** A series over a
  Chart Source draws a line: `level`, `rateOverInterval` or `amountPerPeriod`, read from `x` and `y`. An
  observation's points are each one point, and a price range's candles are merged by period, not
  thinned; a colour, a size, a key, edges and an interval are per-point, and a thinned point stands for
  many rows. A labels axis needs every category, which only the whole column knows. Each of these is
  refused with its path, as any spec error is.
- **`ChartSpec.ReadAsync(json, sources, pixels, from, to)` reads a spec over sources:** each series is
  asked for its own column (two series over one table ask twice, since each keeps its own extremes),
  all at once, and the reading is refused if their answers name different versions of one data — two
  series of one chart are never drawn from two moments (spine rule 6).
- **A Board asks for 2,048 pixels** (`BoundBoard.ChartPixels`): the device pixels of a plot 1,024 CSS
  pixels wide at the chart's resolution (ExChart counts two device pixels a CSS pixel, ADR-0254). A
  plot wider than that thins on top in the browser, which keeps every extreme the source kept; a
  source asked for the plot's own width would have to be asked again whenever the layout moved.
- **A Board's chart over a server input is asked again as the user narrows it:** a link's narrowing,
  handed to the source as a grid's is (ADR-0343), and the range the user narrows the chart to, whose
  ends become `From` and `To` so the narrowed range is drawn at full detail. Asking is asynchronous: the
  chart keeps what it shows until the answer comes, an answer to an ask a later one has replaced is
  dropped, and one older than the version the chart shows is never drawn (ADR-0322). A source that
  fails says why in the chart's place.
- **`BoardSource.Thin` is how a host's source answers a chart**, handed a `BoardChartQuery`: the view,
  the Chart Source query and the narrowing. A Board with a chart over a server input whose
  source has no `Thin` is refused at binding, by path.

## Consequences

- ExChart.Engine gains `IChartSource`, `ChartSourceQuery`, `ChartSourceAnswer`, `ChartSource` and
  `ChartSpec.ReadAsync`; `ChartSeries` gains `Thinned`.
- ExBoard gains `BoardSource.Thin`, `BoardChartQuery` and `BoundBoard.ChartPixels`; a chart view over
  a server input is drawn.
- A Chart Spec answered by the AI tool (`draw_chart`) still reads Data Handles held in memory; a host
  whose handles are on a server reads with `ReadAsync`.
