# exposure-chart — a disposable proof of concept

What charting an ExGrid selection would look like, before any ADR is written: an exposure profile
(Date × EPE(0) / ENE(0) / CVA, each Before / After / Diff, under Header Groups), a selection
turned into a chart, and the chart's data exported as CSV. **Nothing here is a decision**; it is
the material for the grilling that follows it. Throw it away once that is done.

```sh
nix develop -c dotnet run --project spikes/exposure-chart -- --urls http://localhost:5410
```

Three pages: `/` the exposure profile, `/cds` a CDS curve and the hazard rates bootstrapped from
it, `/curves` several reference entities' curves with their tenors across.
Both draw through one component, `Components/ChartBox.razor`, which takes the grid's columns,
its Header Groups, the data, the window and the selection, and the Consumer's declarations:
which columns read on the right, their units and colours, and their styles.

Select **Date** and the figures (drag, or click a header and Ctrl+click others), then right-click
→ **Chart selection**, or the button. One selected cell charts the whole table, as in Excel.

![Select, chart, filter, compare](screenshots/poc.gif)

![Add series to a chart already drawn](screenshots/add-series.gif)

| | |
|---|---|
| ![Panel per group, hovered](screenshots/hover.png) | ![One axis](screenshots/one-axis.png) |
| ![Chart Filters](screenshots/filter.png) | ![Tooltip](screenshots/tooltip.png) |
| ![Steps and markers, first five years](screenshots/step-5y.png) | ![Style panel](screenshots/style-panel.png) |
| ![Every date, yyyy-MM-dd at 90°](screenshots/x-every-date-90.png) | ![Tenor ticks and labels](screenshots/x-tenors.png) |
| ![Every date at 45°, first five years](screenshots/x-every-date-45-5y.png) | ![Axis panel](screenshots/x-every-date-45-5y-panel.png) |
| ![ENE(0) added over its own six dates](screenshots/own-dates.png) | ![One column over two stretches of rows](screenshots/own-dates-gap.png) |

## The CDS page

![CDS spreads and hazard rates](screenshots/cds-page.png)

Par spreads by tenor read on the left axis (bp), drawn as points joined; the hazard rates read on
the right (% a year), drawn as what they are — piecewise constant over (tᵢ₋₁, tᵢ], filled where
the rate holds, open where it does not. The bootstrap (`Analytics/CdsData.cs`) prices each maturity's CDS at
par with the earlier intervals fixed: 40% recovery, a flat 3% rate, quarterly premiums on the IMM
dates (ACT/360) with half a period's accrual on default, the protection leg exact on each interval,
and the first coupon counted from today since the buyer is paid back what accrued before it (the
first hazard rate, 0.7073%, is s / (1 − R) at ACT/360). A first row at the curve's date carries no
quote and no hazard: it is where the first interval starts. The axis' ticks are the pillars,
labelled by the grid's Tenor column (`XLabel.Column`) — a tenor counted from today would read
"1894D" for a 5Y maturity on the roll. "Quotes ±10bp" bootstraps again, and the chart follows.

Writing the page, the grid refused it by name: a Header Group straddled the pinned column
(ADR-0032). The page pinned nothing instead.

## The curves page: series in rows

![CDS curves, tenors across](screenshots/curves-page.png)

| | |
|---|---|
| ![Two tickers selected](screenshots/curves-two.png) | ![An inverted curve](screenshots/curves-deltap.png) |

One row per reference entity — ticker, name, currency, seniority, doc clause, recovery — and its
tenors across, under two Header Groups: the par spreads, and the hazard rates bootstrapped with
the row's own recovery (`Analytics/CdsCurves.cs`; where no positive rate prices a quote the rate is left
empty, with every later one, rather than invented). This is Excel's "series in rows", and the
chart reads it so (`RowChartDefinition`, `RowChartSpec`):

- **A series is a row and a Header Group**: an entity's spreads, its hazard rates — one colour per
  row, a line with points for the spreads and a step over (tᵢ₋₁, tᵢ] from 0 for the hazard rates.
  It is named by the row's key and follows the row through a sort.
- **The axis is the tenors' own years, declared per column** — "6m" is a header's text, 0.5 is what
  the page says it stands for — so 6m to 1y is a tenth of 10y to 15y, not equal to it, as on
  Excel's category axis. The horizontal axis became a number with a scale (`XScale`): a date is
  its day number, a tenor its years.
- **Which way a selection is read is declared, not guessed from its shape.** Selected rows across
  tenor columns chart those tenors; rows with no tenor column (their tickers) chart their whole
  curves. A number column with no position — the recovery rate — selected with tenors is refused
  by name: it was chosen, and this axis has no place for it.

## The legend

| | |
|---|---|
| ![A table legend](screenshots/legend-table.png) | ![Hidden from the legend](screenshots/legend-hidden.png) |
| ![The legend on the right](screenshots/legend-right.png) | ![The exposure page's table legend](screenshots/legend-exposure.png) |

The page's legend is HTML beside the drawing, not part of it, so that it can be clicked; the
downloaded SVG carries its own. Filter → Legend sets where it stands (top, right, bottom, none)
and its form: a list, one entry per series, or a table — a colour per row, a drawing per column
(the curves page: each entity × its spreads and hazard rates; the exposure page: Before / After /
Diff × EPE, ENE, CVA, PFE). Auto chooses the table only when every cell of it holds a series: a
table with holes reads worse than the list. A click hides an entry, a row's header or a column's
header hides all it names, and a click on something wholly hidden shows it again; hidden entries
stay in the legend, faded, so they can be found. It is the filter's own state, so both agree.
Each sample is drawn as its series is — a line, points, a step's open and filled ends.

## What is computed where

The profile, the bootstrap and the hazard rates are under `Analytics/`, which stands for the risk
system: computed outside the chart and the grid, and put in the grid's rows by the page, as a
Consumer would put what its analytics returned (decided with the user, 2026-10-13: a bootstrap
belongs to analytics, never to the chart package). Nothing under `Charting/` or `ChartBox`
refers to it. The chart knows a hazard rate only by its column's declarations — the right axis,
"% a year", a step over (tᵢ₋₁, tᵢ] — which is the analytics' convention, said by the Consumer.

## How it is built

- **No change to the core.** The context menu's Consumer command (ADR-0036) already carries the
  selected ranges and the Row Sequence Version they were taken under; that is all a chart needs.
- **The chart is defined in the data's terms** (`ChartDefinition`): the axis column, and series
  each made of a column and **dates of its own** (decided with the user, 2026-10-13), as each Excel
  series has its own ranges. A sort or a recalculation leaves it meaning the same thing; a stale
  version is refused (spine rule 6). Ranges selected over different rows no longer refuse: each
  column is charted at the rows its ranges covered. The axis spans every series' dates, with a
  hover band at each; a series with no point at a date reads "—" there, which is not "(no value)".
- **A line joins only neighbours in the data.** A series charted over two stretches of rows is
  not drawn across the rows between them, whose values it does not show; a step's interval runs
  to the data's previous or next date, the bucket being the data's, not the series'.
- **Nothing is guessed.** Headers are not data; series names and panels come from the Header
  Groups ("EPE(0) · Before"); the axis is the one Date column, and a selection without one, with
  two, or with a text column is refused by name.
- **SVG written in C#** (`ChartSvg`). The hover guide and the dots are CSS `:hover`, instant and
  with no circuit traffic. The downloaded file carries a native `<title>` per date instead of a
  tooltip, since it has no page to draw one.
- **The tooltip is one listener, in ADR-0029's shape** (`wwwroot/exposure-chart.js`; the user
  allowed widening the JavaScript allowlist for it, 2026-10-13 — the ADR is for the grilling to
  write). It reports the date under the pointer **only when it changes** and when the pointer
  leaves — 48 frames on the circuit for 89 mousemoves across 44 dates — and never measures: Blazor
  draws the tooltip at a percentage of the chart's width that C# computed. Each report carries the
  drawing's version, and one taken against an earlier drawing (a filter changed meanwhile) is
  dropped, not shown against the new one (spine rule 6). Values read through the column's own
  format, so the tooltip and the cell say the same digits.
- **Chart Filters, as Excel's funnel beside a chart** (`ChartView`): series by checkbox, by
  Header Group, or by leaf across every group ("hide every Before"); dates by a From/To range or a
  horizon (1Y / 5Y / 10Y / All) rather than Excel's one checkbox per category, which for 67 dates
  is a list nobody reads. It narrows the chart and its CSV, never the grid or the definition, and
  a hidden series keeps its colour slot so "After" stays blue when "Before" goes.
- **How points are joined is declared per series** (`SeriesStyle`, chosen per Header Group under
  Filter → Style): a line, a line with points, points only, or a step — over (tᵢ₋₁, tᵢ] or over
  [tᵢ, tᵢ₊₁). A step is drawn as mathematics draws one: the end that takes the value is a
  filled marker, the end that does not is an open one, and where the next level is equal the
  function is continuous and no open marker is drawn; there is no vertical riser, since the
  function takes no value between the levels. Markers are a circle, a square, a diamond or a
  triangle. CVA defaults to the (tᵢ₋₁, tᵢ] step, because that is the piecewise-constant profile
  its bucket-by-bucket integral uses; a line between the dates would show a profile the
  calculation never saw.
- **The horizontal axis is set in three independent choices** (`XAxisStyle`, Filter → Horizontal
  axis): where the ticks go (calendar boundaries, every date in the data, or the market's tenors
  from the first date — 1M, 3M, 6M, 1Y, 2Y, 5Y, 10Y …), how they are labelled (yyyy, yyyy-MM,
  yyyy-MM-dd, MMM yyyy, or a tenor, "18M", "5Y"), and at what angle (0°, 45°, 90°, read upwards
  as in Excel). A label that would touch its neighbour is left out — never overprinted — and
  the panel says how many of the ticks are labelled; every tick is still drawn. The room under
  the last panel grows with the angle and the longest label. Labels are not measured: an
  estimate per character decides both (ADR-0021).
- **Series are added to a chart already drawn** (Excel's Select Data → Add): "+ Add series" lists
  the number columns not yet in it, by Header Group — added at the chart's dates, since a list of
  columns names no rows — or adds the grid's current selection, each column at the rows it covered. A new group
  becomes a new panel, and its Before / After / Diff take the colours those leaves already have.
- **Two CSVs.** The wide one (a row per date, a column per series) cannot tell an empty cell
  that is a missing value from one where the series is not charted; the long one (Series, Date,
  Value — a row per point) can, its empty Value being only ever a missing one.
- **CSV with no script**: a `data:` URL behind `<a download>`, UTF-8 with a byte-order mark (Excel
  on a Japanese Windows otherwise reads Shift_JIS), invariant, unformatted, every digit; a missing
  value is an empty field, never a zero.

## What it showed

1. **One axis is unreadable for this table**, as predicted: CVA (tens of thousands) lies flat on
   an axis scaled by EPE (millions). A panel per Header Group is the right default.
2. **A right-hand axis for Diff costs half a panel.** Aligning the two zeros (so that no false
   level is drawn) stretches the left axis down to the Diff's negative extent: EPE(0) reads
   against −25M…15M. A Diff panel of its own may be better than a secondary axis.
3. **A per-bucket figure drawn as a line misleads.** CVA here is each bucket's contribution, and
   the tenor grid widens from monthly to quarterly to yearly, so the line jumps where the bucket
   width does. A series needs a declared kind — a level (EPE) or an amount per period (CVA by
   bucket), drawn as steps or bars, or normalised by the bucket's length.
4. **The tooltip was the weak point of "no JavaScript"**: a native `<title>` appears after about a
   second and cannot be styled. It is now a listener (above). On the Server host the tooltip
   follows the pointer one round trip behind; the guide line, being CSS, does not.
5. **Text is not measured**, so the legend wraps on an estimate per character (ADR-0021).
6. **The rows a selection names are the shown rows.** Under a filter, the chart is of what was
   visible; "the whole table" means the Window, not the Consumer's data.
7. **A gesture must carry only what it changes.** The style controls first sent the whole style
   they were rendered with; on the Server host, choosing a Draw and then a Marker within one round
   trip put the Draw back. Each control now changes its one field on the style as it is when the
   event is handled (spine rule 6) — the kind of race only machine-speed driving shows.
8. **Charts beyond this one** (risk ladders by tenor, PL Explain waterfalls, HPL-vs-RTPL scatter for
   PLA) put a category or a number on the horizontal axis; the category's order then matters,
   and this PoC does not answer it.
