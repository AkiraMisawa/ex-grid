# A chart is read aloud as a summary, and can be read as a table

*Decided with the user, 2026-10-19 (M2 grilling Q8). Extends ADR-0033 to ExChart.*

A screen reader meets a chart as one image. **ExChart gives it a summary written from the Chart
Definition**: the chart's name, each axis with its range ("Date, 2026-10 to 2046-10"; "Amount"), and
each visible series with its maximum, written through the column's format. It is generated, so a
Consumer who writes nothing still gets a chart that says what it is; a Consumer may add a description.

**"View as table" shows what the chart shows as a table**: the same content as the CSV (grilling Q5,
M1) — series and range after Chart Filters, every point before Thinning — in an HTML table a screen
reader walks by its own commands. Highcharts' accessibility module takes the same approach.

## Considered

- **The Consumer's description only**: a chart nobody described says nothing.
- **Point-by-point keyboard navigation**: the table already lets a reader reach every value, and
  walking a thousand points by arrow key is not how anyone reads a series. Not in the first version.

*(2026-10-19, [ADR-0283](./0283-exchart-ships-on-the-familys-train-measured-and-checked-by-machine.md),
decided with the user: the summary and the table are fixed by an ARIA snapshot in layer 3, not read by
a person; what a screen reader actually speaks is stated as not verified.)*

*Note of 2026-11-09, found in the review of the legend's keyboard fix (#549):* **the open table stands while
the chart draws again.** Every rebuild — a series toggled from the legend, new data, a range narrowed, the
axis switched — replaced the whole table block, so a reader scrolled down a long table was sent back to its
first row, and one who had tabbed into its scroller lost the keyboard to the page's body. Now the block, its
caption and its rows stay, and only the rows and cells whose text changed are rewritten, as the drawing's
parts are (ADR-0252); a 3D surface's table does the same. When the block itself must be replaced — the
table turned into the notice that the chart was thinned at its source (ADR-0344), or back — a keyboard it
held is handed to the block drawn in its place, or, where that takes none, to the "View as table" button of
the built-in toolbar, never to the page's body, as ADR-0271's note of the same day does for the legend.
