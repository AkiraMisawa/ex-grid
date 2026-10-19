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
