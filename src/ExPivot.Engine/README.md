# ExPivot.Engine

The pivot engine of ExPivot, with no UI and no dependency. It aggregates **Source Records** under a
**Pivot Layout** — which fields stand in Filters, Rows, Columns and Values, with each one's
settings — into a **Pivot Report**: the rows, the label columns, the value columns and the header
rectangles over them, as Excel's PivotTable lays them out.

- **Items** are a field's distinct values: text told apart ignoring case, numbers by value, dates
  by their clock value, a Blank as `(blank)`, ordered as Excel orders them, or by a Value Field.
- **Hidden Items** leave their records out of every cell and every total.
- **Aggregations**: Sum, Count, Average, Max, Min, Product, Count Numbers, StdDev, StdDevp, Var,
  Varp. A total is aggregated from its records, never from the totals below it, and money stays
  exact: integral and `decimal` values are summed in `decimal`.
- **The three report forms** — Compact, Outline, Tabular — with subtotals at the top or the
  bottom, grand totals, collapsed Items, several Value Fields (**Σ Values**) in rows or columns,
  and Show Values As (% of Grand Total, % of Column Total, % of Row Total).

The engine's behaviour is the specification of ExPivot's semantics (ADR-0059 in the repository):
a server that answers a pivot itself is held to it.

> **This is a prerelease (`0.x`).** ExPivot is built alongside ExGrid and is not part of its
> release. The API may change between prereleases.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0` and has no dependencies.

## Install

```sh
dotnet add package ExPivot.Engine --prerelease
```

## Computing a report

```csharp
using System.Globalization;
using ExPivot.Engine;

record Sale(string Region, string Product, decimal Amount);

var sales = new[]
{
    new Sale("East", "Apples", 100m),
    new Sale("East", "Pears", 50m),
    new Sale("West", "Apples", 70m),
};
PivotField<Sale>[] fields =
[
    new("Region", PivotFieldType.Text, s => s.Region),
    new("Product", PivotFieldType.Text, s => s.Product),
    new("Amount", PivotFieldType.Number, s => s.Amount),
];
var layout = new PivotLayout
{
    Rows = [new PivotFieldPlacement("Region")],
    Columns = [new PivotFieldPlacement("Product")],
    Values = [new PivotValueField("Amount", PivotAggregation.Sum)],
};

var report = PivotEngine.Compute(sales, fields, layout,
    new PivotOptions { Culture = CultureInfo.GetCultureInfo("en-US") });

report.ValueColumns.Select(c => c.Header);   // Apples, Pears, Grand Total
report.Rows[0].Labels[0].Text;               // East
report.Rows[0].ValueAt(0)!.Text;             // 100
report.Rows[^1].ValueAt(2)!.Text;            // 220 — the grand total
```

`PivotEngine.Aggregate` makes the pass over the records and returns a `PivotCube`;
`PivotEngine.Report` lays a cube out. A layout that changes only how the result is laid out —
collapsing an Item, sorting, the form, the totals, an Aggregation, a format — is laid out from
the same cube (`PivotCube.Holds`), with no pass over the records.

## The layout's rules and its saved form

`PivotLayoutEdits` holds the Field List's rules as functions from one layout to the next: ticking
a field, dropping it on an Area, moving and removing an entry, hiding Items, sorting, collapsing,
and a Value Field's settings. `PivotLayoutJson` writes a layout as versioned JSON and reads it back,
refusing a version it does not know.
